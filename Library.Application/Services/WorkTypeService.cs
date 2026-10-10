using Library.Application.Abstractions;
using Library.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

/// <summary>
/// 作品類型的業務規則。由 WorkTypesController 呼叫。
/// </summary>
public class WorkTypeService
{
    private readonly ILibraryDbContext _db;

    public WorkTypeService(ILibraryDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 所有類型，依 SortOrder 排序。WPF 以第一個作為新增作品的預設類型。
    /// </summary>
    public async Task<List<WorkTypeDto>> GetAllAsync()
    {
        // SortOrder 可能相同，最後依 Id 排順序才固定
        var types = _db.WorkTypes.OrderBy(t => t.SortOrder).ThenBy(t => t.Id).AsNoTracking();
        var list = await types.Select(t => new WorkTypeDto(t.Id, t.Name, t.SortOrder)).ToListAsync();
        return list;
    }
}
