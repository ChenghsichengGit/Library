using Library.Application.Abstractions;
using Library.Application.Dtos;
using Library.Application.Exceptions;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

/// <summary>
/// 系列的查詢與修改。新增由 WorkService 存作品時順便建立。
/// </summary>
public class SeriesService(ILibraryDbContext db, ICoverStorage storage, CoverCleaner cleaner)
{
    /// <summary>
    /// 有作品的系列，附作品數與作者（只算作者、不含社團），依名稱排序。已刪除的作品由 Query Filter 排除。
    /// </summary>
    public async Task<List<SeriesDto>> GetAllAsync(int? creatorId = null)
    {
        var series = db.Series.Where(s => s.Works.Any());
        // 只列這個人（不分角色）有參與的系列
        if (creatorId != null)
            series = series.Where(s => s.Works.Any(w => w.Creators.Any(wc => wc.CreatorId == creatorId)));

        var rows = await series
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.Note,
                s.CoverKey,
                WorkCount = s.Works.Count,
                Authors = s.Works
                    .SelectMany(w => w.Creators)
                    .Where(wc => wc.Role == CreatorRole.Author)
                    .Select(wc => wc.Creator.Name)
                    .Distinct()
                    .ToList(),
                // 沒設定封面時用：系列裡最新一部有封面的作品
                LatestCoverKey = s.Works
                    .Where(w => w.CoverKey != "")
                    .OrderByDescending(w => w.CreatedAt)
                    .Select(w => w.CoverKey)
                    .FirstOrDefault()
            })
            .ToListAsync();

        // GetUrl 翻不成 SQL，拿回記憶體再組；Distinct 之後的順序 SQL 不保證，作者名也在這裡排序
        return rows.Select(r =>
        {
            var cover = r.CoverKey != "" ? r.CoverKey : r.LatestCoverKey;
            return new SeriesDto(r.Id, r.Name, r.WorkCount, r.Authors.Order().ToArray(), r.Note,
                r.CoverKey == "" ? null : r.CoverKey,
                cover == null ? null : storage.GetUrl(cover));
        }).ToList();
    }

    /// <summary>修改名稱、備註、封面。找不到回傳 false；和別的系列同名時丟出 NameConflictException。</summary>
    public async Task<bool> UpdateAsync(int id, SaveSeriesRequest request)
    {
        var series = await db.Series.FindAsync(id);
        if (series == null)
            return false;

        var name = request.Name!.Trim();
        // 排除自己：只改大小寫不算重複
        if (await db.Series.AnyAsync(s => s.Id != id && s.Name == name))
            throw new NameConflictException("已有相同名稱");

        var oldCoverKey = series.CoverKey;
        series.Name = name;
        series.Note = request.Note?.Trim() ?? "";
        series.CoverKey = request.CoverKey ?? "";

        await db.SaveChangesAsync();
        await cleaner.DeleteIfUnusedAsync(oldCoverKey, series.CoverKey);
        return true;
    }

    /// <summary>系列裡有封面的作品，新的在前，最多 60 張。</summary>
    public async Task<List<CoverChoiceDto>> GetCoverChoicesAsync(int id)
    {
        var rows = await db.Works
            .Where(w => w.CoverKey != "" && w.SeriesId == id)
            .OrderByDescending(w => w.CreatedAt)
            .Take(60)
            .Select(w => new { w.Id, w.Title, w.CoverKey })
            .ToListAsync();

        return rows.Select(r => new CoverChoiceDto(r.Id, r.Title, r.CoverKey, storage.GetUrl(r.CoverKey))).ToList();
    }
}
