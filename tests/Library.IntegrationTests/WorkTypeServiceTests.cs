using Library.Application.Dtos.Requests;
using Library.Application.Exceptions;
using Library.Application.Services;
using Library.Domain.Entities;
using Library.Infrastructure.Data;
using Library.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests;

/// <summary>
/// WorkTypeService 的整合測試。種子資料：漫畫 0、動畫 1、影片 2、遊戲 3、ASMR 4、其他 99。
/// </summary>
[Collection("SqlServer")]
public class WorkTypeServiceTests(SqlServerFixture fixture) : DatabaseTestBase(fixture)
{
    private static WorkTypeService CreateService(LibraryDbContext db) => new(db);

    // 防：排序被改壞，WPF 以第一個作為預設類型
    [Fact]
    public async Task GetAllAsync_ReturnsTypesInSortOrder()
    {
        await using var db = CreateDbContext();

        var result = await CreateService(db).GetAllAsync();

        Assert.Equal(["漫畫", "動畫", "影片", "遊戲", "ASMR", "其他"], result.Select(t => t.Name));
    }

    // 防：SortOrder 少了 + 1，和 ASMR 同順序
    [Fact]
    public async Task CreateAsync_WithoutSortOrder_PlacesBeforeOther()
    {
        await using (var db = CreateDbContext())
        {
            var dto = await CreateService(db).CreateAsync(new SaveWorkTypeRequest { Name = " 同人誌 " });

            Assert.Equal("同人誌", dto.Name);
            Assert.Equal(5, dto.SortOrder);
        }

        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db).GetAllAsync();

            Assert.Equal(["同人誌", "其他"], result.TakeLast(2).Select(t => t.Name));
        }
    }

    // 防：名稱重複沒擋；SQL Server 不分大小寫，asmr 和 ASMR 算重複
    [Fact]
    public async Task CreateAsync_DuplicateName_Throws()
    {
        await using var db = CreateDbContext();

        await Assert.ThrowsAsync<WorkTypeConflictException>(() =>
            CreateService(db).CreateAsync(new SaveWorkTypeRequest { Name = "asmr" }));
    }

    // 防：名稱重複的檢查沒有排除自己，名稱不變只改順序也被擋
    [Fact]
    public async Task UpdateAsync_SameName_ChangesSortOrder()
    {
        await using (var db = CreateDbContext())
        {
            var result = await CreateService(db)
                .UpdateAsync(2, new SaveWorkTypeRequest { Name = "動畫", SortOrder = 10 });

            Assert.True(result);
        }

        await using (var db = CreateDbContext())
        {
            var type = await db.WorkTypes.SingleAsync(t => t.Id == 2);

            Assert.Equal("動畫", type.Name);
            Assert.Equal(10, type.SortOrder);
        }
    }

    // 防：刪除的條件沒有比對要刪的 id，結果「其他」被刪掉或所有類型都刪不掉
    [Fact]
    public async Task DeleteAsync_Other_Throws()
    {
        await using var db = CreateDbContext();

        await Assert.ThrowsAsync<WorkTypeConflictException>(() => CreateService(db).DeleteAsync(WorkType.OtherId));
    }

    // 防：漏了 IgnoreQueryFilters，最近刪除裡的作品沒搬走，刪除時撞上外鍵變成 500
    [Fact]
    public async Task DeleteAsync_MovesWorksIncludingSoftDeletedToOther()
    {
        int typeId;
        await using (var db = CreateDbContext())
        {
            var type = await CreateService(db).CreateAsync(new SaveWorkTypeRequest { Name = "測試" });
            typeId = type.Id;

            db.Works.Add(new Work { TitleJa = "一般", WorkTypeId = typeId });
            db.Works.Add(new Work { TitleJa = "已刪除", WorkTypeId = typeId, DeletedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDbContext())
        {
            Assert.True(await CreateService(db).DeleteAsync(typeId));
        }

        await using (var db = CreateDbContext())
        {
            var works = await db.Works.IgnoreQueryFilters().ToListAsync();

            Assert.Equal(2, works.Count);
            Assert.All(works, w => Assert.Equal(WorkType.OtherId, w.WorkTypeId));
            Assert.Null(await db.WorkTypes.FindAsync(typeId));
        }
    }
}