using Library.Application.Dtos.Requests;
using Library.Application.Exceptions;
using Library.Application.Services;
using Library.Domain.Entities;
using Library.Infrastructure.Data;
using Library.IntegrationTests.Support;

namespace Library.IntegrationTests;

/// <summary>
/// CreatorService 的整合測試：清單、修改（改名、封面）與封面候選。
/// </summary>
[Collection("SqlServer")]
public class CreatorServiceTests(SqlServerFixture fixture) : DatabaseTestBase(fixture)
{
    private const string OldKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg";
    private const string NewKey = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.png";

    private FakeCoverStorage Covers { get; } = new();

    private CreatorService CreateService(LibraryDbContext db) => new(db, Covers, new CoverCleaner(db, Covers));

    private static WorkCreator As(Creator creator, CreatorRole role) => new() { Creator = creator, Role = role };

    // Valve 同時是作者和社團；Zeta 先建立（Id 較小），數量相同時依 Id 排會錯；Ghost 只出現在已刪除的作品
    private async Task SeedAsync()
    {
        await using var db = CreateDbContext();
        var zeta = new Creator { Name = "Zeta" };
        var valve = new Creator { Name = "Valve" };
        var ea = new Creator { Name = "EA" };
        var alpha = new Creator { Name = "Alpha" };
        var ghost = new Creator { Name = "Ghost" };

        db.Works.Add(new Work { TitleJa = "Zeta の作品", WorkTypeId = 1, Creators = [As(zeta, CreatorRole.Author)] });
        db.Works.Add(new Work
        {
            TitleJa = "Portal 2", WorkTypeId = 1,
            Creators = [As(valve, CreatorRole.Author), As(valve, CreatorRole.Circle)]
        });
        db.Works.Add(new Work
        {
            TitleJa = "Half-Life", WorkTypeId = 1,
            Creators = [As(valve, CreatorRole.Author), As(ea, CreatorRole.Circle)]
        });
        db.Works.Add(new Work { TitleJa = "Counter-Strike", WorkTypeId = 1, Creators = [As(valve, CreatorRole.Circle)] });
        db.Works.Add(new Work { TitleJa = "Alpha の作品", WorkTypeId = 1, Creators = [As(alpha, CreatorRole.Author)] });
        db.Works.Add(new Work
        {
            TitleJa = "已刪除", WorkTypeId = 1, DeletedAt = DateTime.UtcNow,
            Creators = [As(valve, CreatorRole.Author), As(ghost, CreatorRole.Author)]
        });
        await db.SaveChangesAsync();
    }

    // 作者「Mio」：1/1 a、1/3 b、1/5 沒封面、1/7 當社團的 c、1/9 已刪除的 d
    private async Task<int> SeedCoversAsync()
    {
        await using var db = CreateDbContext();
        var mio = new Creator { Name = "Mio" };
        Work WorkOn(int day, string coverKey, CreatorRole role, bool deleted = false) => new()
        {
            TitleJa = $"1/{day}", WorkTypeId = 1, CoverKey = coverKey,
            CreatedAt = new DateTime(2026, 1, day, 0, 0, 0, DateTimeKind.Utc),
            DeletedAt = deleted ? DateTime.UtcNow : null,
            Creators = [As(mio, role)]
        };
        db.Works.AddRange(
            WorkOn(1, "a.jpg", CreatorRole.Author),
            WorkOn(3, "b.jpg", CreatorRole.Author),
            WorkOn(5, "", CreatorRole.Author),
            WorkOn(7, "c.jpg", CreatorRole.Circle),
            WorkOn(9, "d.jpg", CreatorRole.Author, deleted: true));
        await db.SaveChangesAsync();
        return mio.Id;
    }

    private async Task<int> IdOfAsync(string name)
    {
        await using var db = CreateDbContext();
        return db.Creators.Single(c => c.Name == name).Id;
    }

    private async Task<Creator> FindAsync(int id)
    {
        await using var db = CreateDbContext();
        return (await db.Creators.FindAsync(id))!;
    }

    private async Task<bool> UpdateAsync(int id, SaveCreatorRequest request)
    {
        await using var db = CreateDbContext();
        return await CreateService(db).UpdateAsync(id, request);
    }

    // ── 清單 ──

    // 防：Count 沒加角色條件（Valve 會變成 4）、算到已刪除的作品、列出只當社團的人、數量相同時沒依名稱排序
    [Fact]
    public async Task GetAllAsync_Authors_CountsOnlyAuthorRoleOnActiveWorks()
    {
        await SeedAsync();

        await using var db = CreateDbContext();
        var result = await CreateService(db).GetAllAsync(CreatorRole.Author);

        Assert.Equal(["Valve", "Alpha", "Zeta"], result.Select(c => c.Name));
        Assert.Equal([2, 1, 1], result.Select(c => c.WorkCount));
    }

    // 防：Where 沒加角色條件，只當作者的 Alpha、Zeta 也被列成社團
    [Fact]
    public async Task GetAllAsync_Circles_CountsOnlyCircleRole()
    {
        await SeedAsync();

        await using var db = CreateDbContext();
        var result = await CreateService(db).GetAllAsync(CreatorRole.Circle);

        Assert.Equal(["Valve", "EA"], result.Select(c => c.Name));
        Assert.Equal([2, 1], result.Select(c => c.WorkCount));
    }

    // 防：預設封面挑到沒封面的、別的角色的、已刪除的作品，或排序方向反了；有設定時沒用自己的
    [Fact]
    public async Task GetAllAsync_CoverFallsBackToLatestWorkCoverOfRole()
    {
        var id = await SeedCoversAsync();

        await using (var db = CreateDbContext())
        {
            var mio = (await CreateService(db).GetAllAsync(CreatorRole.Author)).Single();
            Assert.Null(mio.CoverKey);
            Assert.Equal("fake/b.jpg", mio.CoverUrl);
        }

        await UpdateAsync(id, new SaveCreatorRequest { Name = "Mio", CoverKey = OldKey });

        await using (var db = CreateDbContext())
        {
            var mio = (await CreateService(db).GetAllAsync(CreatorRole.Author)).Single();
            Assert.Equal(OldKey, mio.CoverKey);
            Assert.Equal($"fake/{OldKey}", mio.CoverUrl);
        }
    }

    // 防：沒有任何封面時回傳 "fake/" 這種壞網址
    [Fact]
    public async Task GetAllAsync_NoCoverAnywhere_ReturnsNullUrl()
    {
        await SeedAsync();

        await using var db = CreateDbContext();
        var result = await CreateService(db).GetAllAsync(CreatorRole.Author);

        Assert.All(result, c => Assert.Null(c.CoverUrl));
    }

    // ── 修改 ──

    // 防：撞名檢查沒排除自己（名稱不變就存不了）、欄位沒寫入、備註沒 Trim
    [Fact]
    public async Task UpdateAsync_SameName_SavesFields()
    {
        await SeedAsync();
        var id = await IdOfAsync("Valve");

        var ok = await UpdateAsync(id, new SaveCreatorRequest
        {
            Name = "Valve", Url = " https://www.valvesoftware.com/ ", Note = " Steam の会社 ", CoverKey = OldKey
        });

        Assert.True(ok);
        var saved = await FindAsync(id);
        Assert.Equal("https://www.valvesoftware.com/", saved.Url);
        Assert.Equal("Steam の会社", saved.Note);
        Assert.Equal(OldKey, saved.CoverKey);
    }

    // 防：撞名沒擋下（資料庫的唯一索引會丟出例外變成 500）；比對要不分大小寫
    [Fact]
    public async Task UpdateAsync_NameTakenByOther_Throws()
    {
        await SeedAsync();
        var id = await IdOfAsync("Alpha");

        await Assert.ThrowsAsync<NameConflictException>(() =>
            UpdateAsync(id, new SaveCreatorRequest { Name = "zeta" }));
    }

    // 防：只改大小寫被當成撞到自己
    [Fact]
    public async Task UpdateAsync_CaseOnlyRename_Succeeds()
    {
        await SeedAsync();
        var id = await IdOfAsync("Valve");

        Assert.True(await UpdateAsync(id, new SaveCreatorRequest { Name = " VALVE " }));
        Assert.Equal("VALVE", (await FindAsync(id)).Name);
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsFalse()
    {
        Assert.False(await UpdateAsync(9999, new SaveCreatorRequest { Name = "誰" }));
    }

    // 防：沒清舊封面，或在 SaveChanges 之前清（那時資料庫還是舊 key，會判斷成有人在用）
    [Fact]
    public async Task UpdateAsync_ReplacedCover_DeletesOldFile()
    {
        await SeedAsync();
        var id = await IdOfAsync("Alpha");
        await UpdateAsync(id, new SaveCreatorRequest { Name = "Alpha", CoverKey = OldKey });

        await UpdateAsync(id, new SaveCreatorRequest { Name = "Alpha", CoverKey = NewKey });

        Assert.Equal([OldKey], Covers.Deleted);
    }

    // ── 封面候選 ──

    // 防：候選分角色（漏了當社團的 c）、列出沒封面或已刪除的作品、沒有新的在前
    [Fact]
    public async Task GetCoverChoicesAsync_ListsActiveWorkCoversOfAnyRole_NewestFirst()
    {
        var id = await SeedCoversAsync();

        await using var db = CreateDbContext();
        var choices = await CreateService(db).GetCoverChoicesAsync(id);

        Assert.Equal(["c.jpg", "b.jpg", "a.jpg"], choices.Select(c => c.CoverKey));
        Assert.Equal(["fake/c.jpg", "fake/b.jpg", "fake/a.jpg"], choices.Select(c => c.CoverUrl));
        Assert.Equal(["1/7", "1/3", "1/1"], choices.Select(c => c.Title));
    }
}
