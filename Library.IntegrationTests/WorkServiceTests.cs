using Library.Application.Dtos;
using Library.Application.Services;
using Library.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Library.IntegrationTests;

/// <summary>
/// WorkService 的整合測試：搭配真的 SQL Server（Testcontainers 開的 Docker 容器）執行。
/// </summary>
/// <remarks>
/// 每個測試的流程：建構子決定資料庫名稱 → InitializeAsync 建庫並跑 Migration → 測試 → DisposeAsync 刪庫。
/// 驗證時一律用新的 DbContext 重新讀取：同一個 DbContext 可能直接給你記憶體裡的物件，
/// 這樣就抓不到「忘了 SaveChangesAsync」這種錯誤。
/// 這裡不經過 DI 容器，直接 new WorkService(db, 假時鐘)。
/// </remarks>
// [Collection("SqlServer")]：和其他整合測試共用同一個 SQL Server 容器（見 SqlServerCollection）
[Collection("SqlServer")]
public class WorkServiceTests : IAsyncLifetime
{

    // 每個測試各用一個隨機命名的資料庫，測試之間不會互相影響
    private readonly string _connectionString;
    // 假時鐘固定在 2026-01-01 12:00 UTC，時間相關的斷言才能精確比對
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    private LibraryDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LibraryDbContext>().UseSqlServer(_connectionString).Options);

    private WorkService CreateService(LibraryDbContext db) => new(db, _time);

    // xUnit 每個測試都會建立一個新的 WorkServiceTests，並把共用的 SqlServerFixture 傳進來
    // 容器給的連線字串指向 master，這裡換成這個測試專用的資料庫名稱
    public WorkServiceTests(SqlServerFixture fixture)
    {
        _connectionString = new SqlConnectionStringBuilder(fixture.ConnectionString)
        {
            InitialCatalog = $"LibraryTest_{Guid.NewGuid():N}"
        }.ConnectionString;
    }

    
    // 每個測試開始前：建立資料庫，並用正式的 Migration 建表
    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    // 每個測試結束後：刪除資料庫
    public async Task DisposeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }

    // ↓ 測試寫在這裡
    [Fact]
    public async Task CreateAsync_SetsCreatedAtToCurrentTime()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var dto = await service.CreateAsync(new SaveWorkRequest { TitleJa = "テスト" });

        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0), dto.CreatedAt);
    }

    [Fact]
    public async Task CreateAsync_TrimsTitlesAndSaves()
    {
        int id;
        await using (var db = CreateDbContext())
        {
            var dto = await CreateService(db).CreateAsync(new SaveWorkRequest { TitleJa = "  テスト  " });
            id = dto.Id;
        }

        // 用新的 DbContext 讀，確保讀到的是資料庫裡真正存的值，而不是記憶體裡的物件
        await using var verifyDb = CreateDbContext();
        var saved = await verifyDb.Works.SingleAsync(w => w.Id == id);

        Assert.Equal("テスト", saved.TitleJa);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.GetByIdAsync(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsFalse()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.UpdateAsync(999, new SaveWorkRequest { TitleJa = "A" });

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFalse()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var result = await service.DeleteAsync(999);

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        int id;
        await using (var db = CreateDbContext())
        {
            var dto = await CreateService(db).CreateAsync(new SaveWorkRequest { TitleJa = "  テスト  " });
            id = dto.Id;
        }

        _time.Advance(TimeSpan.FromDays(1));
        
        await using (var db2 = CreateDbContext())
        {
            var service1 = CreateService(db2);
            var success = await service1.DeleteAsync(id);
            Assert.True(success);
        }


        // 用新的 DbContext 讀，確保讀到的是資料庫裡真正存的值，而不是記憶體裡的物件
        await using var verifyDb = CreateDbContext();
        var service = CreateService(verifyDb);

        // 一般查詢查不到（全域過濾器排除了已刪除的作品）
        var found = await service.GetByIdAsync(id);
        // IgnoreQueryFilters：暫時關掉全域過濾器，才查得到已刪除的資料，確認資料還在
        var result = await verifyDb.Works.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == id);

        Assert.Null(found);
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 1, 2, 12, 0, 0), result.DeletedAt);
    }
    
    [Fact]
    public async Task UpdateAsync_ReplacesAllFields_AndKeepsCreatedAt()
    {
        // 第 1 段：新增一筆「有日文名稱、有上架日期、評分 5」的作品，記下 id
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var dto = await service.CreateAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            ReleaseDate = new DateOnly(2025, 10, 10),
            Score = 5
        });

        var id = dto.Id;
        // 讓時間往前走一天（這樣如果建立時間被改掉，就會變成 01-02，測試抓得到）
        _time.Advance(TimeSpan.FromDays(1));

        // 第 2 段：修改成「只有英文名稱、沒有上架日期、評分 3」，檢查回傳 true
        var titleEn = "TEST";
        await using var db1 = CreateDbContext();
        var service1 = CreateService(db1);
        await service1.UpdateAsync(id, new SaveWorkRequest
        {
            TitleEn = titleEn,
            Score = 3
        });

        // 第 3 段：用新的 DbContext 讀出來，檢查：
        //   - TitleJa 變成 ""（被清掉了）
        //   - TitleEn 是新的值
        //   - ReleaseDate 是 null（被清掉了）
        //   - Score 是 3
        //   - CreatedAt 還是 2026-01-01 12:00（沒被改掉）
        await using var db2 = CreateDbContext();
        var result = await db2.Works.SingleAsync(w => w.Id == id);
        
        Assert.Empty(result.TitleJa);
        Assert.Equal(titleEn, result.TitleEn);
        Assert.Null(result.ReleaseDate);
        Assert.Equal(3, result.Score);
        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0), result.CreatedAt);
    }
    
    [Fact]
    public async Task GetWorksAsync_FiltersBySearchText_InAnyTitleOrRemark()
    {
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            await service.CreateAsync(new SaveWorkRequest { TitleJa = "魔法少女" });
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "Magic Girl", Remark = "魔法" });
            await service.CreateAsync(new SaveWorkRequest { TitleZh = "其他作品" });
        }

        await using var verifyDb = CreateDbContext();
        var result = await CreateService(verifyDb).GetWorksAsync(new WorkQuery { Q = "魔法" });

        // 第 1 筆比對到日文名稱，第 2 筆比對到備註，第 3 筆不符合
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetWorksAsync_NoFilter_ReturnsAll()
    {
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            await service.CreateAsync(new SaveWorkRequest { TitleJa = "魔法少女" });
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "Magic Girl", Remark = "魔法" });
            await service.CreateAsync(new SaveWorkRequest { TitleZh = "其他作品" });
        }

        await using var verifyDb = CreateDbContext();
        var result = await CreateService(verifyDb).GetWorksAsync(new WorkQuery {});

        Assert.Equal(3, result.Count);
    }
    [Fact]
    public async Task GetWorksAsync_FiltersByScoreRange()
    {
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            await service.CreateAsync(new SaveWorkRequest { TitleJa = "魔法少女", Score = 0});
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "Magic Girl", Remark = "魔法", Score = 3});
            await service.CreateAsync(new SaveWorkRequest { TitleZh = "其他作品", Score = 5});
            await service.CreateAsync(new SaveWorkRequest { TitleZh = "其他作品2", Score = 6});
        }

        await using var verifyDb = CreateDbContext();
        var result = await CreateService(verifyDb).GetWorksAsync(new WorkQuery {MinScore = 3, MaxScore = 5});

        // 只檢查筆數不夠：條件寫反時（篩出 0 和 6）筆數也是 2，所以也要檢查每筆的評分
        Assert.Equal(2, result.Count);
        Assert.All(result, w => Assert.InRange(w.Score, 3, 5));
    }
    
    // 主要名稱的規則在資料庫的計算欄位裡，只能用整合測試驗證（原本的單元測試 WorkTests 已移除）
    // expected 是事先寫好的標準答案，測試裡不能去改它
    // 沒有「三個都空」這組：CHECK 約束讓這種作品存不進資料庫
    [Theory]
    [InlineData("中文", "日本語", "English", "中文")]
    [InlineData("", "日本語", "English", "日本語")]
    [InlineData("", "", "English", "English")]
    public async Task CreateAsync_SetsTitleFromFirstNonEmptyName(string zh, string ja, string en, string expected)
    {
        int id;
        
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            var work = await service.CreateAsync(new SaveWorkRequest { TitleZh = zh, TitleJa = ja, TitleEn = en });
            id =  work.Id;
        }
        // 用新的 DbContext 讀，拿到的是資料庫計算並存起來的 Title
        await using var verifyDb = CreateDbContext();
        var result = await CreateService(verifyDb).GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal(expected, result.Title);
    }

    [Fact]
    public async Task GetWorksAsync_SortsByTitle()
    {
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            await service.CreateAsync(new SaveWorkRequest { TitleJa = "C"});
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "B"});
            await service.CreateAsync(new SaveWorkRequest { TitleZh = "A"});
        }
        
        await using var verifyDb = CreateDbContext();
        var result = await CreateService(verifyDb).GetWorksAsync(new WorkQuery { Sort = WorkSort.Title, Desc = false});
        
        Assert.Equal(["A", "B", "C"], result.Select(w => w.Title));
    }

    [Fact]
    // 降冪時沒有上架日的也要在最後；Assert.Equal 比較兩個序列時，順序也要一樣才會通過
    public async Task GetWorksAsync_SortsByReleaseDate_NullsLast()
    {
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            await service.CreateAsync(new SaveWorkRequest { TitleZh = "A", ReleaseDate = new DateOnly(2025, 3, 10)});
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "B"});
            await service.CreateAsync(new SaveWorkRequest { TitleJa = "C", ReleaseDate = new DateOnly(2025, 1, 1)});
        }
        
        await using var verifyDb = CreateDbContext();
        var result = await CreateService(verifyDb).GetWorksAsync(new WorkQuery { Sort = WorkSort.ReleaseDate, Desc = true});
        
        Assert.Equal([new DateOnly(2025, 3, 10), new DateOnly(2025, 1, 1), null], result.Select(w => w.ReleaseDate));
    }

    // 防：GetByIdAsync 漏了 Include／ThenInclude（作者變成空陣列或 NullReferenceException）、名字沒整理、沒排序
    [Fact]
    public async Task CreateAsync_SavesAuthorsAndCircles()
    {
        int id;
        await using (var db = CreateDbContext())
        {
            var dto = await CreateService(db).CreateAsync(new SaveWorkRequest
            {
                TitleJa = "テスト",
                Authors = ["B作者", " A作者 ", "", "a作者"],
                Circles = ["某社團"]
            });
            id = dto.Id;
        }

        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetByIdAsync(id);

            // 空字串被濾掉、前後空白被去掉、a作者 和 A作者 視為同一個，結果依名字排序
            Assert.Equal(["A作者", "B作者"], result!.Authors);
            Assert.Equal(["某社團"], result.Circles);
        }
    }

    // 防：UpdateAsync 漏了 Include，舊的關聯清不掉，結果變成 A 和 B 都在
    [Fact]
    public async Task UpdateAsync_ReplacesAuthors()
    {
        int id;
        await using (var db = CreateDbContext())
        {
            var dto = await CreateService(db).CreateAsync(new SaveWorkRequest
            {
                TitleJa = "テスト",
                Authors = ["A"]
            });
            id = dto.Id;
        }

        await using (var db = CreateDbContext())
        {
            // PUT 是整筆取代，名稱也要給，否則會撞上資料庫的 CHECK 約束（這裡不經過 Controller 的驗證）
            var result = await CreateService(db).UpdateAsync(id, new SaveWorkRequest
            {
                TitleJa = "テスト",
                Authors = ["B"]
            });

            Assert.True(result);
        }
        
        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetByIdAsync(id);

            Assert.Equal(["B"], result!.Authors);
        }
    }

    // 防：新建的 Creator 沒放回 Dictionary，同一個新名字被建立兩次而撞上唯一索引
    // Creators 表只有一筆（人），WorkCreators 有兩筆（同一個人的兩個角色）
    [Fact]
    public async Task CreateAsync_SameNameAsAuthorAndCircle_CreatesOneCreator()
    {
        int id;
        await using (var db = CreateDbContext())
        {
            var dto = await CreateService(db).CreateAsync(new SaveWorkRequest
            {
                TitleJa = "テスト",
                Authors = ["Valve"],
                Circles = ["Valve"]
            });
            id = dto.Id;
        }

        await using (var db = CreateDbContext())
        {
            Assert.Single(await db.Creators.ToListAsync());     // 人只有一個

            var result = await CreateService(db).GetByIdAsync(id);
            Assert.Equal(["Valve"], result!.Authors);          // 而且同時是作者
            Assert.Equal(["Valve"], result.Circles);           // 也是社團
        }
    }

    // 作品 B 的名稱不含 Valve，被找到只可能是因為作者名稱符合
    [Fact]
    public async Task GetWorksAsync_FiltersBySearchText_InCreatorName()
    {
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            await service.CreateAsync(new SaveWorkRequest { TitleJa = "A", Authors = ["S"], Circles = ["C", "T"] });
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "B", Authors = ["Valve"] });
        }

        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetWorksAsync(new WorkQuery {Q = "Valve"});
            var work = Assert.Single(result);
            Assert.Equal("B", work.Title);
        }
    }
}