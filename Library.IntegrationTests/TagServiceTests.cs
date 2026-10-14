using Library.Application.Services;
using Library.Domain.Entities;
using Library.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests;

/// <summary>
/// TagService 的整合測試。
/// </summary>
[Collection("SqlServer")]
public class TagServiceTests(SqlServerFixture fixture) : DatabaseTestBase(fixture)
{
    private static TagService CreateService(LibraryDbContext db) => new(db);

    // 防：列出沒在用的標籤、算到已刪除的作品、排序錯誤（數量相同時 B 的 Id 比 A 小，依 Id 排會錯）
    [Fact]
    public async Task GetAllAsync_CountsActiveWorksOnly_AndSorts()
    {
        await using (var db = CreateDbContext())
        {
            var b = new Tag { Name = "B" };
            var c = new Tag { Name = "C" };
            db.Works.Add(new Work { TitleJa = "1", WorkTypeId = 1, Tags = [b, c] });
            await db.SaveChangesAsync();

            var a = new Tag { Name = "A" };
            var d = new Tag { Name = "D" };
            db.Works.Add(new Work { TitleJa = "2", WorkTypeId = 1, Tags = [a, c] });
            db.Works.Add(new Work { TitleJa = "已刪除", WorkTypeId = 1, DeletedAt = DateTime.UtcNow, Tags = [a, d] });
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetAllAsync();

            Assert.Equal(["C", "A", "B"], result.Select(t => t.Name));
            Assert.Equal([2, 1, 1], result.Select(t => t.WorkCount));
        }
    }

    // 防：刪除標籤後作品還留著它
    [Fact]
    public async Task DeleteAsync_RemovesTagFromWorks()
    {
        int workId, tagId;
        await using (var db = CreateDbContext())
        {
            var a = new Tag { Name = "A" };
            var work = new Work { TitleJa = "1", WorkTypeId = 1, Tags = [a, new Tag { Name = "B" }] };
            db.Works.Add(work);
            await db.SaveChangesAsync();
            workId = work.Id;
            tagId = a.Id;
        }

        await using (var db = CreateDbContext())
        {
            Assert.True(await CreateService(db).DeleteAsync(tagId));
        }

        await using (var db = CreateDbContext())
        {
            var work = await db.Works.Include(w => w.Tags).SingleAsync(w => w.Id == workId);

            Assert.Equal(["B"], work.Tags.Select(t => t.Name));
            Assert.Null(await db.Tags.FindAsync(tagId));
        }
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsFalse()
    {
        await using var db = CreateDbContext();

        Assert.False(await CreateService(db).DeleteAsync(999));
    }
}
