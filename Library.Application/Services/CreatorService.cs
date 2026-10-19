using Library.Application.Abstractions;
using Library.Application.Dtos;
using Library.Application.Exceptions;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

/// <summary>
/// 作者、社團的查詢與修改。新增由 WorkService 存作品時順便建立。
/// </summary>
public class CreatorService(ILibraryDbContext db, ICoverStorage storage, CoverCleaner cleaner)
{
    /// <summary>以這個角色參與作品的人與作品數，多的在前。已刪除的作品由 WorkCreator 的 Query Filter 排除。</summary>
    public async Task<List<CreatorDto>> GetAllAsync(CreatorRole role)
    {
        var rows = await db.Creators
            .Where(c => c.Works.Any(wc => wc.Role == role))
            .OrderByDescending(c => c.Works.Count(wc => wc.Role == role))
            .ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id, c.Name, c.Url, c.Note, c.CoverKey,
                WorkCount = c.Works.Count(wc => wc.Role == role),
                // 沒設定封面時用：這個角色最新一部有封面的作品
                LatestCoverKey = c.Works
                    .Where(wc => wc.Role == role && wc.Work.CoverKey != "")
                    .OrderByDescending(wc => wc.Work.CreatedAt)
                    .Select(wc => wc.Work.CoverKey)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return rows.Select(r =>
        {
            var cover = r.CoverKey != "" ? r.CoverKey : r.LatestCoverKey;
            return new CreatorDto(r.Id, r.Name, r.WorkCount, r.Url, r.Note,
                r.CoverKey == "" ? null : r.CoverKey,
                cover == null ? null : storage.GetUrl(cover));
        }).ToList();
    }

    /// <summary>修改名稱、網址、備註、封面。找不到回傳 false；和別人同名時丟出 NameConflictException。</summary>
    public async Task<bool> UpdateAsync(int id, SaveCreatorRequest request)
    {
        var creator = await db.Creators.FindAsync(id);
        if (creator == null)
            return false;

        var name = request.Name!.Trim();
        // 排除自己：只改大小寫（zun → ZUN）不算重複
        if (await db.Creators.AnyAsync(c => c.Id != id && c.Name == name))
            throw new NameConflictException("已有相同名稱");

        var oldCoverKey = creator.CoverKey;
        creator.Name = name;
        creator.Url = request.Url?.Trim() ?? "";
        creator.Note = request.Note?.Trim() ?? "";
        creator.CoverKey = request.CoverKey ?? "";

        await db.SaveChangesAsync();
        // 存檔之後才判斷：存檔前查詢，資料庫裡還是舊的 key，一定會判斷成「有人在用」
        await cleaner.DeleteIfUnusedAsync(oldCoverKey, creator.CoverKey);
        return true;
    }

    /// <summary>這個人參與（不分角色）的作品中有封面的，新的在前，最多 60 張。</summary>
    public async Task<List<CoverChoiceDto>> GetCoverChoicesAsync(int id)
    {
        var rows = await db.Works
            .Where(w => w.CoverKey != "" && w.Creators.Any(wc => wc.CreatorId == id))
            .OrderByDescending(w => w.CreatedAt)
            .Take(60)
            .Select(w => new { w.Id, w.Title, w.CoverKey })
            .ToListAsync();

        return rows.Select(r => new CoverChoiceDto(r.Id, r.Title, r.CoverKey, storage.GetUrl(r.CoverKey))).ToList();
    }
}
