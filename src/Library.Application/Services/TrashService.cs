using System.Linq.Expressions;
using Library.Application.Abstractions;
using Library.Application.Dtos.Responses;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

/// <summary>
/// 最近刪除：列出、還原、永久刪除軟刪除的作品。
/// </summary>
public class TrashService(ILibraryDbContext db, ICoverStorage storage, CoverCleaner cleaner, TimeProvider time)
{
    public const int KeepDays = 30;

    // 已刪除的作品。全域篩選會擋掉它們（連 Creators 這種關聯也會被擋），所以先關掉篩選
    private IQueryable<Work> Deleted => db.Works.IgnoreQueryFilters().Where(w => w.DeletedAt != null);

    /// <summary>已刪除的作品，最近刪除的在前。</summary>
    public async Task<TrashDto> GetAsync()
    {
        var items = await Deleted
            .OrderByDescending(w => w.DeletedAt)
            .Select(w => new TrashItemDto(
                w.Id,
                w.Title,
                w.WorkType.Name,
                w.Creators.Where(wc => wc.Role == CreatorRole.Author).Select(wc => wc.Creator.Name).ToArray(),
                // 最外層的 Select 可以呼叫一般的 C# 方法：EF 先查出資料，再在記憶體裡執行
                w.CoverKey == "" ? null : storage.GetUrl(w.CoverKey),
                w.DeletedAt!.Value))
            .ToArrayAsync();

        return new TrashDto(KeepDays, items);
    }

    /// <summary>還原：清掉刪除時間，作品回到清單。不在最近刪除裡時回傳 false。</summary>
    public async Task<bool> RestoreAsync(int id)
    {
        var work = await Deleted.FirstOrDefaultAsync(w => w.Id == id);
        if (work is null)
            return false;

        work.DeletedAt = null;
        // 刪除時有更新修改時間，還原也算一次修改
        work.UpdatedAt = time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>永久刪除一部。不在最近刪除裡時回傳 false（沒刪除的作品不能直接永久刪除）。</summary>
    public async Task<bool> PurgeAsync(int id) => await PurgeWhereAsync(w => w.Id == id) > 0;

    /// <summary>清空最近刪除，回傳刪了幾部。</summary>
    public Task<int> EmptyAsync() => PurgeWhereAsync(_ => true);

    /// <summary>永久刪除「刪除超過 KeepDays 天」的作品，回傳刪了幾部。API 啟動時執行。</summary>
    public Task<int> PurgeExpiredAsync()
    {
        var cutoff = time.GetUtcNow().UtcDateTime.AddDays(-KeepDays);
        return PurgeWhereAsync(w => w.DeletedAt < cutoff);
    }

    // 網址、DLC、作者與標籤的關聯由資料庫的 Cascade 一起刪；封面沒有人用了才刪檔
    private async Task<int> PurgeWhereAsync(Expression<Func<Work, bool>> filter)
    {
        var works = Deleted.Where(filter);

        // 刪除前先記下封面，刪完就查不到了
        var coverKeys = await works.Where(w => w.CoverKey != "").Select(w => w.CoverKey).Distinct().ToListAsync();

        // 直接送出 DELETE，不用先把作品讀進記憶體
        var count = await works.ExecuteDeleteAsync();

        foreach (var key in coverKeys)
            await cleaner.DeleteIfUnusedAsync(key, "");
        return count;
    }
}
