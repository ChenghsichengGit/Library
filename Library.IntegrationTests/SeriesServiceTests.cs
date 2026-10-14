using Library.Application.Services;
using Library.Domain.Entities;
using Library.Infrastructure.Data;

namespace Library.IntegrationTests;

/// <summary>
/// SeriesService 的整合測試。
/// </summary>
[Collection("SqlServer")]
public class SeriesServiceTests(SqlServerFixture fixture) : DatabaseTestBase(fixture)
{
    private static SeriesService CreateService(LibraryDbContext db) => new(db);

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
}
