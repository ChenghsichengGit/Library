using Library.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

/// <summary>
/// 換封面後刪掉舊檔。作品、作者／社團、系列可能共用同一個檔案，都沒人用才刪。
/// </summary>
public class CoverCleaner(ILibraryDbContext db, ICoverStorage storage)
{
    public async Task DeleteIfUnusedAsync(string oldKey, string newKey)
    {
        if (oldKey == "" || oldKey == newKey)
            return;

        // 已刪除的作品也要算進去：還原時還要用到它的封面
        var used = await db.Works.IgnoreQueryFilters().AnyAsync(w => w.CoverKey == oldKey)
                   || await db.Series.AnyAsync(s => s.CoverKey == oldKey)
                   || await db.Creators.AnyAsync(c => c.CoverKey == oldKey);
        if (!used)
            await storage.DeleteAsync(oldKey);
    }
}
