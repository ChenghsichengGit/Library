using Library.Application.Dtos.Requests;
using Library.Application.Dtos.Responses;
using Library.Application.Exceptions;
using Library.Application.Services;
using Library.Domain.Entities;
using Library.Infrastructure.Data;
using Library.IntegrationTests.Support;

namespace Library.IntegrationTests;

/// <summary>
/// SeriesService 的整合測試：清單、修改（改名、封面）與封面候選。
/// </summary>
[Collection("SqlServer")]
public class SeriesServiceTests(SqlServerFixture fixture) : DatabaseTestBase(fixture)
{
    private const string OldKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg";
    private const string NewKey = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.png";

    private FakeCoverStorage Covers { get; } = new();

    private SeriesService CreateService(LibraryDbContext db) => new(db, Covers, new CoverCleaner(db, Covers));

    // 「東方」：1/1 a、1/3 b、1/5 沒封面、1/9 已刪除的 d；「西方」：1/7 c（別的系列，不能被挑到）
    private async Task<(int Touhou, int Seihou)> SeedCoversAsync()
    {
        await using var db = CreateDbContext();
        var touhou = new Series { Name = "東方" };
        var seihou = new Series { Name = "西方" };
        Work WorkOn(int day, string coverKey, Series series, bool deleted = false) => new()
        {
            TitleJa = $"1/{day}", WorkTypeId = 1, CoverKey = coverKey, Series = series,
            CreatedAt = new DateTime(2026, 1, day, 0, 0, 0, DateTimeKind.Utc),
            DeletedAt = deleted ? DateTime.UtcNow : null
        };
        db.Works.AddRange(
            WorkOn(1, "a.jpg", touhou),
            WorkOn(3, "b.jpg", touhou),
            WorkOn(5, "", touhou),
            WorkOn(7, "c.jpg", seihou),
            WorkOn(9, "d.jpg", touhou, deleted: true));
        await db.SaveChangesAsync();
        return (touhou.Id, seihou.Id);
    }

    // 東方：ZUN 當作者；西方：ZUN 當社團；Portal：無關；幽霊：ZUN 只在已刪除的作品
    private async Task<int> SeedSeriesOfCreatorAsync()
    {
        await using var db = CreateDbContext();
        var zun = new Creator { Name = "ZUN" };
        var valve = new Creator { Name = "Valve" };
        var ghost = new Series { Name = "幽霊" };
        Work WorkIn(string series, Creator creator, CreatorRole role) => new()
        {
            TitleJa = series, WorkTypeId = 1, Series = new Series { Name = series },
            Creators = [new WorkCreator { Creator = creator, Role = role }]
        };
        db.Works.AddRange(
            WorkIn("東方", zun, CreatorRole.Author),
            WorkIn("西方", zun, CreatorRole.Circle),
            WorkIn("Portal", valve, CreatorRole.Author),
            new Work
            {
                TitleJa = "幽霊1", WorkTypeId = 1, Series = ghost,
                Creators = [new WorkCreator { Creator = valve, Role = CreatorRole.Author }]
            },
            new Work
            {
                TitleJa = "幽霊2", WorkTypeId = 1, Series = ghost, DeletedAt = DateTime.UtcNow,
                Creators = [new WorkCreator { Creator = zun, Role = CreatorRole.Author }]
            });
        await db.SaveChangesAsync();
        return zun.Id;
    }

    private async Task<Series> FindAsync(int id)
    {
        await using var db = CreateDbContext();
        return (await db.Series.FindAsync(id))!;
    }

    private async Task<bool> UpdateAsync(int id, SaveSeriesRequest request)
    {
        await using var db = CreateDbContext();
        return await CreateService(db).UpdateAsync(id, request);
    }

    private async Task<SeriesDto> GetOneAsync(string name)
    {
        await using var db = CreateDbContext();
        return (await CreateService(db).GetAllAsync()).Single(s => s.Name == name);
    }

    // ── 清單 ──

    // 防：列出沒有作品的系列、算到已刪除的作品、作者混入社團或重複、沒依名稱排序
    [Fact]
    public async Task GetAllAsync_CountsActiveWorksAndAuthors_SortedByName()
    {
        await using (var db = CreateDbContext())
        {
            var a = new Series { Name = "A" };
            var b = new Series { Name = "B" };
            var c = new Series { Name = "C" };
            var x = new Creator { Name = "X" };
            var z = new Creator { Name = "Z" };
            var w = new Creator { Name = "W" };

            // B：兩部作品，作者 X 重複、Y 是社團
            db.Works.Add(new Work
            {
                TitleJa = "1", WorkTypeId = 1, Series = b,
                Creators =
                [
                    new WorkCreator { Creator = x, Role = CreatorRole.Author },
                    new WorkCreator { Creator = new Creator { Name = "Y" }, Role = CreatorRole.Circle }
                ]
            });
            db.Works.Add(new Work
            {
                TitleJa = "2", WorkTypeId = 1, Series = b,
                Creators =
                [
                    new WorkCreator { Creator = z, Role = CreatorRole.Author },
                    new WorkCreator { Creator = x, Role = CreatorRole.Author }
                ]
            });
            // A：一部沒有作者的作品，作者 W 只在已刪除的作品裡
            db.Works.Add(new Work { TitleJa = "3", WorkTypeId = 1, Series = a });
            db.Works.Add(new Work
            {
                TitleJa = "4", WorkTypeId = 1, Series = a, DeletedAt = DateTime.UtcNow,
                Creators = [new WorkCreator { Creator = w, Role = CreatorRole.Author }]
            });
            // C：只有已刪除的作品
            db.Works.Add(new Work { TitleJa = "5", WorkTypeId = 1, Series = c, DeletedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetAllAsync();

            Assert.Equal(["A", "B"], result.Select(s => s.Name));
            Assert.Equal([1, 2], result.Select(s => s.WorkCount));
            Assert.Empty(result[0].Authors);
            Assert.Equal(["X", "Z"], result[1].Authors);
        }
    }

    // 防：creatorId 沒套用到查詢、只看作者角色（漏了西方）、算到已刪除的作品（多了幽霊）
    [Fact]
    public async Task GetAllAsync_WithCreatorId_ListsSeriesTheyTookPartIn()
    {
        var zun = await SeedSeriesOfCreatorAsync();

        await using var db = CreateDbContext();
        var result = await CreateService(db).GetAllAsync(zun);

        Assert.Equal(["東方", "西方"], result.Select(s => s.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task GetAllAsync_WithoutCreatorId_ListsAll()
    {
        await SeedSeriesOfCreatorAsync();

        await using var db = CreateDbContext();
        var result = await CreateService(db).GetAllAsync();

        Assert.Equal(["Portal", "幽霊", "東方", "西方"], result.Select(s => s.Name).Order(StringComparer.Ordinal));
    }

    // 防：預設封面挑到沒封面的、別的系列的、已刪除的作品，或排序方向反了；
    // CoverKey 傳成預設封面（WPF 會以為已設定）；有設定時沒用自己的
    [Fact]
    public async Task GetAllAsync_CoverFallsBackToLatestWorkCover()
    {
        var (touhou, _) = await SeedCoversAsync();

        var before = await GetOneAsync("東方");
        Assert.Null(before.CoverKey);
        Assert.Equal("fake/b.jpg", before.CoverUrl);

        await UpdateAsync(touhou, new SaveSeriesRequest { Name = "東方", CoverKey = OldKey });

        var after = await GetOneAsync("東方");
        Assert.Equal(OldKey, after.CoverKey);
        Assert.Equal($"fake/{OldKey}", after.CoverUrl);
    }

    // 防：沒有任何封面時回傳 "fake/" 這種壞網址
    [Fact]
    public async Task GetAllAsync_NoCoverAnywhere_ReturnsNullUrl()
    {
        await using (var db = CreateDbContext())
        {
            db.Works.Add(new Work { TitleJa = "1", WorkTypeId = 1, Series = new Series { Name = "S" } });
            await db.SaveChangesAsync();
        }

        Assert.Null((await GetOneAsync("S")).CoverUrl);
    }

    // ── 修改 ──

    // 防：撞名檢查沒排除自己（名稱不變就存不了）、欄位沒寫入、備註沒 Trim
    [Fact]
    public async Task UpdateAsync_SameName_SavesFields()
    {
        var (touhou, _) = await SeedCoversAsync();

        var ok = await UpdateAsync(touhou, new SaveSeriesRequest { Name = "東方", Note = " 弾幕 ", CoverKey = OldKey });

        Assert.True(ok);
        var saved = await FindAsync(touhou);
        Assert.Equal("弾幕", saved.Note);
        Assert.Equal(OldKey, saved.CoverKey);
    }

    // 防：撞名沒擋下（資料庫的唯一索引會丟出例外變成 500）
    [Fact]
    public async Task UpdateAsync_NameTakenByOther_Throws()
    {
        var (touhou, _) = await SeedCoversAsync();

        await Assert.ThrowsAsync<NameConflictException>(() =>
            UpdateAsync(touhou, new SaveSeriesRequest { Name = "西方" }));
    }

    // 防：只改大小寫被當成撞到自己
    [Fact]
    public async Task UpdateAsync_CaseOnlyRename_Succeeds()
    {
        int id;
        await using (var db = CreateDbContext())
        {
            var series = new Series { Name = "touhou" };
            db.Series.Add(series);
            await db.SaveChangesAsync();
            id = series.Id;
        }

        Assert.True(await UpdateAsync(id, new SaveSeriesRequest { Name = " Touhou " }));
        Assert.Equal("Touhou", (await FindAsync(id)).Name);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsFalse()
    {
        Assert.False(await UpdateAsync(9999, new SaveSeriesRequest { Name = "誰" }));
    }

    // 防：沒清舊封面，或在 SaveChanges 之前清（那時資料庫還是舊 key，會判斷成有人在用）
    [Fact]
    public async Task UpdateAsync_ReplacedCover_DeletesOldFile()
    {
        var (touhou, _) = await SeedCoversAsync();
        await UpdateAsync(touhou, new SaveSeriesRequest { Name = "東方", CoverKey = OldKey });

        await UpdateAsync(touhou, new SaveSeriesRequest { Name = "東方", CoverKey = NewKey });

        Assert.Equal([OldKey], Covers.Deleted);
    }

    // ── 封面候選 ──

    // 防：列出別的系列、沒封面或已刪除的作品，沒有新的在前
    [Fact]
    public async Task GetCoverChoicesAsync_ListsActiveWorkCoversInSeries_NewestFirst()
    {
        var (touhou, _) = await SeedCoversAsync();

        await using var db = CreateDbContext();
        var choices = await CreateService(db).GetCoverChoicesAsync(touhou);

        Assert.Equal(["b.jpg", "a.jpg"], choices.Select(c => c.CoverKey));
        Assert.Equal(["fake/b.jpg", "fake/a.jpg"], choices.Select(c => c.CoverUrl));
        Assert.Equal(["1/3", "1/1"], choices.Select(c => c.Title));
    }
}
