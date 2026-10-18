using Library.Application.Abstractions;
using Library.Application.Dtos;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

/// <summary>
/// 作者、社團的查詢。新增由 WorkService 存作品時順便建立。
/// </summary>
public class CreatorService(ILibraryDbContext db)
{
    /// <summary>以這個角色參與作品的人與作品數，多的在前。已刪除的作品由 WorkCreator 的 Query Filter 排除。</summary>
    public async Task<List<CreatorDto>> GetAllAsync(CreatorRole role)
    {
        return await db.Creators
            .Where(c => c.Works.Any(wc => wc.Role == role))
            .OrderByDescending(c => c.Works.Count(wc => wc.Role == role))
            .ThenBy(c => c.Name)
            .Select(c => new CreatorDto(c.Id, c.Name, c.Works.Count(wc => wc.Role == role)))
            .ToListAsync();
    }
}
