using Library.Application.Dtos;
using Library.Application.Exceptions;
using Library.Application.Services;
using Library.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Library.IntegrationTests;

/// <summary>
/// WorkService 的整合測試，連 Testcontainers 啟動的 SQL Server。
/// </summary>
/// <remarks>
/// 每個測試用一個新資料庫，以正式的 Migration 建表（WorkTypeId = 1 是種子資料「漫畫」）。
/// 驗證一律用新的 DbContext 讀取，才抓得到忘了 SaveChangesAsync 這類錯誤。
/// 不經過 Controller，所以沒有 [ApiController] 的驗證。
/// </remarks>
[Collection("SqlServer")]
public class WorkServiceTests(SqlServerFixture fixture) : DatabaseTestBase(fixture)
{
    // 時間固定在 2026-01-01 12:00 UTC，斷言才能精確比對
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    private WorkService CreateService(LibraryDbContext db) => new(db, _time);

    [Fact]
    public async Task CreateAsync_SetsCreatedAtToCurrentTime()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var dto = await service.CreateAsync(new SaveWorkRequest { TitleJa = "テスト", WorkTypeId = 1 });

        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0), dto.CreatedAt);
    }

    [Fact]
    public async Task CreateAsync_TrimsTitlesAndSaves()
    {
        int id;
        await using (var db = CreateDbContext())
        {
            var dto = await CreateService(db).CreateAsync(new SaveWorkRequest
            {
                TitleJa = "  テスト  ",
                WorkTypeId = 1
            });
            id = dto.Id;
        }

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
            var dto = await CreateService(db).CreateAsync(new SaveWorkRequest
            {
                TitleJa = "  テスト  ",
                WorkTypeId = 1
            });
            id = dto.Id;
        }

        _time.Advance(TimeSpan.FromDays(1));

        await using (var db2 = CreateDbContext())
        {
            var service1 = CreateService(db2);
            var success = await service1.DeleteAsync(id);
            Assert.True(success);
        }

        await using var verifyDb = CreateDbContext();
        var service = CreateService(verifyDb);

        // 一般查詢查不到，關掉全域過濾器才查得到：資料還在，只是被標記
        var found = await service.GetByIdAsync(id);
        var result = await verifyDb.Works.IgnoreQueryFilters().FirstOrDefaultAsync(w => w.Id == id);

        Assert.Null(found);
        Assert.NotNull(result);
        Assert.Equal(new DateTime(2026, 1, 2, 12, 0, 0), result.DeletedAt);
    }

    [Fact]
    public async Task UpdateAsync_ReplacesAllFields_AndKeepsCreatedAt()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var dto = await service.CreateAsync(new SaveWorkRequest
        {
            TitleJa = "テスト",
            ReleaseDate = new DateOnly(2025, 10, 10),
            Score = 5,
            WorkTypeId = 1
        });

        var id = dto.Id;
        // 時間往前一天：建立時間如果被改掉會變成 01-02
        _time.Advance(TimeSpan.FromDays(1));

        var titleEn = "TEST";
        await using var db1 = CreateDbContext();
        var service1 = CreateService(db1);
        await service1.UpdateAsync(id, new SaveWorkRequest
        {
            TitleEn = titleEn,
            Score = 3,
            WorkTypeId = 1
        });

        // 沒送的欄位被清掉、建立時間保留
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
            await service.CreateAsync(new SaveWorkRequest { TitleJa = "魔法少女", WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "Magic Girl", Remark = "魔法", WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleZh = "其他作品", WorkTypeId = 1 });
        }

        await using var verifyDb = CreateDbContext();
        var result = await CreateService(verifyDb).GetWorksAsync(new WorkQuery { Q = "魔法" });

        // 第 1 筆符合日文名稱，第 2 筆符合備註
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetWorksAsync_NoFilter_ReturnsAll()
    {
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            await service.CreateAsync(new SaveWorkRequest { TitleJa = "魔法少女", WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "Magic Girl", Remark = "魔法", WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleZh = "其他作品", WorkTypeId = 1 });
        }

        await using var verifyDb = CreateDbContext();
        var result = await CreateService(verifyDb).GetWorksAsync(new WorkQuery { });

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetWorksAsync_FiltersByScoreRange()
    {
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            await service.CreateAsync(new SaveWorkRequest { TitleJa = "魔法少女", Score = 0, WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest
                { TitleEn = "Magic Girl", Remark = "魔法", Score = 3, WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleZh = "其他作品", Score = 5, WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleZh = "其他作品2", Score = 6, WorkTypeId = 1 });
        }

        await using var verifyDb = CreateDbContext();
        var result = await CreateService(verifyDb).GetWorksAsync(new WorkQuery { MinScore = 3, MaxScore = 5 });

        // 條件寫反時（篩出 0 和 6）筆數也是 2，所以也檢查每筆的評分
        Assert.Equal(2, result.Count);
        Assert.All(result, w => Assert.InRange(w.Score, 3, 5));
    }

    // 主要名稱由資料庫的計算欄位產生，只能用整合測試驗證；三個都空的作品會被 CHECK 約束擋下
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
            var work = await service.CreateAsync(new SaveWorkRequest
                { TitleZh = zh, TitleJa = ja, TitleEn = en, WorkTypeId = 1 });
            id = work.Id;
        }

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
            await service.CreateAsync(new SaveWorkRequest { TitleJa = "C", WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "B", WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleZh = "A", WorkTypeId = 1 });
        }

        await using var verifyDb = CreateDbContext();
        var result = await CreateService(verifyDb).GetWorksAsync(new WorkQuery { Sort = WorkSort.Title, Desc = false });

        Assert.Equal(["A", "B", "C"], result.Select(w => w.Title));
    }

    // 降冪時沒有上架日的也在最後
    [Fact]
    public async Task GetWorksAsync_SortsByReleaseDate_NullsLast()
    {
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            await service.CreateAsync(new SaveWorkRequest
                { TitleZh = "A", ReleaseDate = new DateOnly(2025, 3, 10), WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "B", WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest
                { TitleJa = "C", ReleaseDate = new DateOnly(2025, 1, 1), WorkTypeId = 1 });
        }

        await using var verifyDb = CreateDbContext();
        var result = await CreateService(verifyDb)
            .GetWorksAsync(new WorkQuery { Sort = WorkSort.ReleaseDate, Desc = true });

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
                Circles = ["某社團"],
                WorkTypeId = 1
            });
            id = dto.Id;
        }

        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetByIdAsync(id);

            // 空字串濾掉、去前後空白、a作者 和 A作者 視為同一個、依名字排序
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
                Authors = ["A"],
                WorkTypeId = 1
            });
            id = dto.Id;
        }

        await using (var db = CreateDbContext())
        {
            // PUT 是整筆取代，名稱也要給，否則會撞上 CHECK 約束
            var result = await CreateService(db).UpdateAsync(id, new SaveWorkRequest
            {
                TitleJa = "テスト",
                Authors = ["B"],
                WorkTypeId = 1
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
                Circles = ["Valve"],
                WorkTypeId = 1
            });
            id = dto.Id;
        }

        await using (var db = CreateDbContext())
        {
            Assert.Single(await db.Creators.ToListAsync());

            var result = await CreateService(db).GetByIdAsync(id);
            Assert.Equal(["Valve"], result!.Authors);
            Assert.Equal(["Valve"], result.Circles);
        }
    }

    // 作品 B 的名稱不含 Valve，被找到只可能是因為作者名稱符合
    [Fact]
    public async Task GetWorksAsync_FiltersBySearchText_InCreatorName()
    {
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            await service.CreateAsync(new SaveWorkRequest
                { TitleJa = "A", Authors = ["S"], Circles = ["C", "T"], WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "B", Authors = ["Valve"], WorkTypeId = 1 });
        }

        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetWorksAsync(new WorkQuery { Q = "Valve" });
            var work = Assert.Single(result);
            Assert.Equal("B", work.Title);
        }
    }

    // 防：GetByIdAsync 漏了 Include(WorkType)、WorkDto 對錯欄位
    [Fact]
    public async Task CreateAsync_SavesWorkType()
    {
        int id;

        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            var dto = await service.CreateAsync(new SaveWorkRequest
                { TitleJa = "A", WorkTypeId = 2 });
            id = dto.Id;
        }

        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetByIdAsync(id);

            Assert.Equal("動畫", result!.WorkType);
            Assert.Equal(2, result!.WorkTypeId);
        }
    }

    // 防：ApplyAsync 讀了 work.WorkTypeId（舊值）而不是 request 的，修改後類型沒變
    [Fact]
    public async Task UpdateAsync_ChangesWorkType()
    {
        int id;

        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            var dto = await service.CreateAsync(new SaveWorkRequest
                { TitleJa = "A", WorkTypeId = 1 });
            id = dto.Id;
        }

        await using (var db2 = CreateDbContext())
        {
            var service = CreateService(db2);
            await service.UpdateAsync(id, new SaveWorkRequest { TitleEn = "A", WorkTypeId = 2 });
        }

        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetByIdAsync(id);

            Assert.Equal(2, result!.WorkTypeId);
        }
    }

    // 防：拿掉類型檢查，存檔時撞上外鍵變成 DbUpdateException（500）
    [Fact]
    public async Task CreateAsync_UnknownWorkType_Throws()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var ex = await Assert.ThrowsAsync<WorkTypeNotFoundException>(() => service.CreateAsync(new SaveWorkRequest
            { TitleJa = "A", WorkTypeId = 999 }));

        Assert.Equal(999, ex.WorkTypeId);
    }

    // 兩種類型的筆數不同（1 和 2），條件寫反時筆數就對不上
    [Fact]
    public async Task GetWorksAsync_FiltersByWorkType()
    {
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "TEST", WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "TEST1", WorkTypeId = 2 });
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "TEST2", WorkTypeId = 2 });
        }

        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetWorksAsync(new WorkQuery { WorkTypeId = 2 });

            Assert.Equal(2, result.Count());
            Assert.Equal(2, result[0].WorkTypeId);
            Assert.Single(result, w => w.Title == "TEST1");
            Assert.Single(result, w => w.Title == "TEST2");
        }
    }

    // 防：GetByIdAsync 漏了 Include(Tags)、名稱沒整理、沒排序
    [Fact]
    public async Task CreateAsync_SavesTags()
    {
        int id;
        await using (var db = CreateDbContext())
        {
            var dto = await CreateService(db).CreateAsync(new SaveWorkRequest
            {
                TitleJa = "テスト",
                WorkTypeId = 1,
                Tags = ["B標籤", " A標籤 ", "", "a標籤"]
            });
            id = dto.Id;
        }

        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetByIdAsync(id);

            // 空字串濾掉、去前後空白、a作者 和 A作者 視為同一個、依名字排序
            Assert.Equal(["A標籤", "B標籤"], result!.Tags);
        }
    }

    // 防：UpdateAsync 漏了 Include，舊的關聯清不掉，結果變成 A 和 B 都在
    [Fact]
    public async Task UpdateAsync_ReplacesTags()
    {
        int id;
        await using (var db = CreateDbContext())
        {
            var dto = await CreateService(db).CreateAsync(new SaveWorkRequest
            {
                TitleJa = "テスト",
                Tags = ["A"],
                WorkTypeId = 1
            });
            id = dto.Id;
        }

        await using (var db = CreateDbContext())
        {
            // PUT 是整筆取代，名稱也要給，否則會撞上 CHECK 約束
            var result = await CreateService(db).UpdateAsync(id, new SaveWorkRequest
            {
                TitleJa = "テスト",
                Tags = ["B"],
                WorkTypeId = 1
            });

            Assert.True(result);
        }

        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetByIdAsync(id);

            Assert.Equal(["B"], result!.Tags);
        }
    }

    // 防：Dictionary 沒設不分大小寫，資料庫查得到 ASMR，但用 asmr 找不到而又新建一筆，撞上唯一索引
    [Fact]
    public async Task CreateAsync_ExistingTagName_ReusesTag()
    {
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            await service.CreateAsync(new SaveWorkRequest { TitleJa = "A", Tags = ["ASMR"], WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleJa = "B", Tags = ["asmr"], WorkTypeId = 1 });
        }

        await using (var db = CreateDbContext())
        {
            // Tags 表只能有一筆
            Assert.Single(await db.Tags.ToListAsync());
        }
    }

    // 防：AND／OR 寫反、預設不是 AND、篩選清單沒整理（空字串、重複被當成條件）
    [Theory]
    [InlineData(null, new[] { "AB" })]
    [InlineData(TagMatch.Any, new[] { "A", "AB" })]
    public async Task GetWorksAsync_FiltersByTags(TagMatch? mode, string[] expected)
    {
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "A", Tags = ["中文"], WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "AB", Tags = ["中文", "長篇"], WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleEn = "C", Tags = ["短篇"], WorkTypeId = 1 });
        }

        await using (var db = CreateDbContext())
        {
            // null 時不設定，測的是預設值
            var query = new WorkQuery { Tags = ["中文", " 長篇 ", "長篇", ""], Sort = WorkSort.Title };
            if (mode is not null)
                query.TagMode = mode.Value;

            var result = await CreateService(db).GetWorksAsync(query);

            Assert.Equal(expected, result.Select(w => w.Title));
        }
    }

    // 防：GetByIdAsync 漏了 Include(Series)、名稱沒去空白、同名（不分大小寫）又建立一個系列
    [Fact]
    public async Task CreateAsync_SavesSeries_AndReusesExisting()
    {
        int firstId, secondId;
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            firstId = (await service.CreateAsync(new SaveWorkRequest
                { TitleJa = "A", Series = " Series ", WorkTypeId = 1 })).Id;
            secondId = (await service.CreateAsync(new SaveWorkRequest
                { TitleJa = "B", Series = "series", WorkTypeId = 1 })).Id;
        }

        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            var first = await service.GetByIdAsync(firstId);
            var second = await service.GetByIdAsync(secondId);

            Assert.Equal("Series", first!.Series);
            Assert.Equal(first.SeriesId, second!.SeriesId);
            Assert.Single(await db.Series.ToListAsync());
        }
    }

    // 防：UpdateAsync 漏了 Include(Series)，設成 null 時 EF Core 看不出變化，系列清不掉
    [Fact]
    public async Task UpdateAsync_EmptySeries_ClearsSeries()
    {
        int id;
        await using (var db = CreateDbContext())
        {
            id = (await CreateService(db).CreateAsync(new SaveWorkRequest
                { TitleJa = "A", Series = "S", WorkTypeId = 1 })).Id;
        }

        await using (var db = CreateDbContext())
        {
            Assert.True(await CreateService(db).UpdateAsync(id, new SaveWorkRequest
                { TitleJa = "A", Series = " ", WorkTypeId = 1 }));
        }

        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetByIdAsync(id);

            Assert.Null(result!.SeriesId);
            Assert.Null(result.Series);
        }
    }

    // 防：搜尋沒比對系列名稱（作品名稱故意不含搜尋字）；沒有系列的作品讓查詢出錯
    [Fact]
    public async Task GetWorksAsync_FiltersBySearchText_InSeriesName()
    {
        await using (var db = CreateDbContext())
        {
            var service = CreateService(db);
            await service.CreateAsync(new SaveWorkRequest { TitleJa = "A", Series = "Half-Life", WorkTypeId = 1 });
            await service.CreateAsync(new SaveWorkRequest { TitleJa = "B", WorkTypeId = 1 });
        }

        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetWorksAsync(new WorkQuery { Q = "Half" });
            var work = Assert.Single(result);
            Assert.Equal("A", work.Title);
        }
    }
}