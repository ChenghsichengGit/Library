using Library.Application.Abstractions;
using Library.Application.Dtos.Requests;
using Library.Application.Dtos.Responses;
using Library.Application.Exceptions;
using Library.Domain.Entities;
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

    /// <summary>
    /// 新增類型。名稱重複時丟出 WorkTypeConflictException；沒給 SortOrder 就排在「其他」前面。
    /// </summary>
    public async Task<WorkTypeDto> CreateAsync(SaveWorkTypeRequest request)
    {
        var name = request.Name!.Trim();

        // 先查而不是交給唯一索引，撞上索引會變成 500；SQL Server 比對不分大小寫
        if (await _db.WorkTypes.AnyAsync(t => t.Name == name))
            throw new WorkTypeConflictException("已有這個類型");

        // 「其他」以外的最大值 + 1，讓「其他」永遠在最後；轉成 int? 是因為 MaxAsync 遇到空集合會丟例外
        var sortOrder = request.SortOrder ?? (await _db.WorkTypes
            .Where(t => t.Id != WorkType.OtherId)
            .MaxAsync(t => (int?)t.SortOrder) ?? -1) + 1;

        var workType = new WorkType
        {
            Name = name,
            SortOrder = sortOrder
        };
        _db.WorkTypes.Add(workType);
        await _db.SaveChangesAsync();

        var dto = new WorkTypeDto(Id: workType.Id, Name: workType.Name, SortOrder: workType.SortOrder);
        return dto;
    }

    /// <summary>
    /// 修改名稱與順序（沒給 SortOrder 就保留原本的）。找不到回傳 false；和別的類型同名時丟出 WorkTypeConflictException。
    /// </summary>
    public async Task<bool> UpdateAsync(int id, SaveWorkTypeRequest request)
    {
        var type = await _db.WorkTypes.FindAsync(id);
        if (type == null)
            return false;

        var name = request.Name!.Trim();
        // 排除自己：名稱不變、只改順序時不算重複
        if (await _db.WorkTypes.AnyAsync(t => t.Id != id && t.Name == name))
            throw new WorkTypeConflictException("已有相同名稱");

        type.Name = name;
        if (request.SortOrder != null)
            type.SortOrder = request.SortOrder.Value;

        await _db.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// 刪除類型，使用它的作品改成「其他」。找不到回傳 false；刪除「其他」時丟出 WorkTypeConflictException。
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        if (id == WorkType.OtherId)
            throw new WorkTypeConflictException("「其他」不能刪除");

        var type = await _db.WorkTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (type == null)
            return false;

        // 最近刪除裡的作品也還連著外鍵，漏掉的話刪除時會被 Restrict 擋下
        var works = await _db.Works
            .IgnoreQueryFilters()
            .Where(w => w.WorkTypeId == id)
            .ToListAsync();
        foreach (var work in works)
            work.WorkTypeId = WorkType.OtherId;

        _db.WorkTypes.Remove(type);
        // 搬移作品和刪除類型在同一個交易裡，中途失敗兩者都不會發生
        await _db.SaveChangesAsync();
        return true;
    }
}
