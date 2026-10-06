using Library.Application.Dtos;
using Library.Application.Services;
using Library.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Library.IntegrationTests;

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

        var found = await service.GetByIdAsync(id);
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

        Assert.Equal(2, result.Count);
        Assert.All(result, w => Assert.InRange(w.Score, 3, 5));
    }
}