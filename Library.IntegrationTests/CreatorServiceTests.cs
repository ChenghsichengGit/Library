using Library.Application.Services;
using Library.Domain.Entities;
using Library.Infrastructure.Data;

namespace Library.IntegrationTests;

/// <summary>
/// CreatorService 的整合測試。
/// </summary>
[Collection("SqlServer")]
public class CreatorServiceTests(SqlServerFixture fixture) : DatabaseTestBase(fixture)
{
    private static CreatorService CreateService(LibraryDbContext db) => new(db);

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
}
