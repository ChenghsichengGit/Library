using Library.Application.Abstractions;
using Library.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

/// <summary>
/// 標籤的查詢與刪除。新增由 WorkService 存作品時順便建立。
/// </summary>
public class TagService
{
    private readonly ILibraryDbContext _db;

    public TagService(ILibraryDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 有作品在用的標籤與作品數，多的在前。已刪除的作品由 Work 的 Query Filter 排除。
    /// </summary>
    public async Task<List<TagDto>> GetAllAsync()
    {
        return await _db.Tags
            .Where(t => t.Works.Any())
            .OrderByDescending(t => t.Works.Count)
            .ThenBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name, t.Works.Count))
            .ToListAsync();
    }

    /// <summary>
    /// 刪除標籤，作品上的關聯由外鍵 Cascade 一起刪除。找不到回傳 false。
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        var tag = await _db.Tags.FindAsync(id);
        if (tag is null)
            return false;

        _db.Tags.Remove(tag);
        await _db.SaveChangesAsync();
        return true;
    }
}