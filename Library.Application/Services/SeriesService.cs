using Library.Application.Abstractions;
using Library.Application.Dtos;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

/// <summary>
/// 系列的查詢。新增由 WorkService 存作品時順便建立。
/// </summary>
public class SeriesService
{
    private readonly ILibraryDbContext _db;

    public SeriesService(ILibraryDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 有作品的系列，附作品數與作者（只算作者、不含社團），依名稱排序。已刪除的作品由 Query Filter 排除。
    /// </summary>
    public async Task<List<SeriesDto>> GetAllAsync()
    {
        var rows = await _db.Series
            .Where(s => s.Works.Any())
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                s.Id,
                s.Name,
                WorkCount = s.Works.Count,
                Authors = s.Works
                    .SelectMany(w => w.Creators)
                    .Where(wc => wc.Role == CreatorRole.Author)
                    .Select(wc => wc.Creator.Name)
                    .Distinct()
                    .ToList()
            })
            .ToListAsync();

        // Distinct 之後的順序 SQL 不保證，作者名拿回記憶體再排序
        return rows
            .Select(r => new SeriesDto(r.Id, r.Name, r.WorkCount, r.Authors.Order().ToArray()))
            .ToList();
    }
}