using Library.Application.Abstractions;
using Library.Application.Dtos.Requests;
using Library.Application.Dtos.Responses;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

/// <summary>
/// 作品的業務規則：查詢、新增、修改、軟刪除。由 WorksController 呼叫。
/// </summary>
public partial class WorkService
{
    private readonly ILibraryDbContext _db;
    private readonly ICoverStorage _cover;
    private readonly CoverCleaner _cleaner;

    // 透過注入取得現在時間，測試才能換成固定的假時鐘
    private readonly TimeProvider _time;

    public WorkService(ILibraryDbContext db, ICoverStorage cover, CoverCleaner cleaner, TimeProvider time)
    {
        _db = db;
        _cover = cover;
        _cleaner = cleaner;
        _time = time;
    }

    /// <summary>
    /// 依條件查詢作品清單並排序。query 裡沒給的條件不篩選。
    /// </summary>
    public async Task<List<WorkDto>> GetWorksAsync(WorkQuery query)
    {
        var works = _db.Works
            .Include(w => w.Creators)
            .ThenInclude(wc => wc.Creator)
            .Include(w => w.WorkType)
            .Include(w => w.Tags)
            .Include(w => w.Series)
            .Include(w => w.Urls)
            .Include(w => w.Dlcs)
            .AsNoTracking();

        // 比對三種語言的名稱而不是 Title，用任一語言都搜得到
        if (!String.IsNullOrEmpty(query.Q))
            works = works.Where(w =>
                w.TitleZh.Contains(query.Q) ||
                w.TitleJa.Contains(query.Q) ||
                w.TitleEn.Contains(query.Q) ||
                w.Description.Contains(query.Q) ||
                w.Creators.Any(wc => wc.Creator.Name.Contains(query.Q)) ||
                (w.Series != null &&
                 w.Series.Name.Contains(query.Q))
            );

        if (query.Favorite != null)
            works = works.Where(w => w.Favorite == query.Favorite);

        if (query.Purchased != null)
            works = works.Where(w => w.Urls.Any(u => u.Purchased) == query.Purchased);

        if (query.WorkTypeId != null)
            works = works.Where(w => w.WorkTypeId == query.WorkTypeId);

        if (query.CreatorId != null)
            works = works.Where(w => w.Creators.Any(wc => wc.CreatorId == query.CreatorId));

        if (query.SeriesId != null)
            works = works.Where(w => w.SeriesId == query.SeriesId);

        if (query.MinScore != null)
            works = works.Where(w => w.Score >= query.MinScore);

        if (query.MaxScore != null)
            works = works.Where(w => w.Score <= query.MaxScore);

        if (query.DlcUnbought)
            works = works.Where(w => w.Dlcs.Any(d => !d.Purchased));

        if (query.OnSale)
            works = works.Where(w => w.Urls.Any(u => u.Discount > 0));

        // 和存檔用同一套整理：清單去重複後，All 才能用「符合數 = 清單長度」判斷
        var tagNames = CleanNames(query.Tags);
        if (tagNames.Count > 0)
        {
            works = query.TagMode == TagMatch.Any
                ? works.Where(w => w.Tags.Any(t => tagNames.Contains(t.Name)))
                : works.Where(w => w.Tags.Count(t => tagNames.Contains(t.Name)) == tagNames.Count);
        }

        var ordered = query.Sort switch
        {
            WorkSort.Title => query.Desc
                ? works.OrderByDescending(w => w.Title)
                : works.OrderBy(w => w.Title),

            // 沒有上架日的不管升降冪都排最後：先依「是不是 null」排，升降冪只套用在日期上
            WorkSort.ReleaseDate => query.Desc
                ? works.OrderBy(w => w.ReleaseDate == null).ThenByDescending(w => w.ReleaseDate)
                : works.OrderBy(w => w.ReleaseDate == null).ThenBy(w => w.ReleaseDate),

            // 評分相同時最近修改的在前，升降冪都一樣（同原版）
            WorkSort.Score => query.Desc
                ? works.OrderByDescending(w => w.Score).ThenByDescending(w => w.UpdatedAt)
                : works.OrderBy(w => w.Score).ThenByDescending(w => w.UpdatedAt),

            WorkSort.UpdatedAt => query.Desc
                ? works.OrderByDescending(w => w.UpdatedAt)
                : works.OrderBy(w => w.UpdatedAt),

            _ => query.Desc
                ? works.OrderByDescending(w => w.CreatedAt)
                : works.OrderBy(w => w.CreatedAt)
        };

        // 排序值相同時資料庫不保證順序，最後依 Id 排才完全確定
        works = ordered.ThenBy(w => w.Id);

        var list = await works.ToListAsync();
        return list.Select(w => WorkDto.From(w, _cover)).ToList();
    }

    /// <summary>
    /// 查詢單筆作品，找不到回傳 null。
    /// </summary>
    public async Task<WorkDto?> GetByIdAsync(int id)
    {
        var work = await _db.Works
            .Include(w => w.Creators)
            .ThenInclude(wc => wc.Creator)
            .Include(w => w.WorkType)
            .Include(w => w.Tags)
            .Include(w => w.Series)
            .Include(w => w.Urls)
            .Include(w => w.Dlcs)
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);

        if (work is null)
            return null;

        return WorkDto.From(work, _cover);
    }

    /// <summary>
    /// 新增作品，回傳建立好的作品。類型不存在時丟出 WorkTypeNotFoundException。
    /// </summary>
    public async Task<WorkDto> CreateAsync(SaveWorkRequest request)
    {
        var work = new Work();
        await ApplyAsync(work, request);

        // 只取一次時間，新增時修改時間和加入時間完全相等
        var time = _time.GetUtcNow().UtcDateTime;
        work.CreatedAt = time;
        work.UpdatedAt = time;

        _db.Works.Add(work);
        await _db.SaveChangesAsync();

        return WorkDto.From(work, _cover);
    }

    /// <summary>
    /// 用 request 整筆取代作品內容（PUT），找不到回傳 false。類型不存在時丟出 WorkTypeNotFoundException。
    /// </summary>
    public async Task<bool> UpdateAsync(int id, SaveWorkRequest request)
    {
        // Include 舊的關聯：Clear() 才刪得掉（網址會被刪除），Series 設成 null 時 EF Core 才看得出變化
        var work = await _db.Works
            .Include(w => w.Creators)
            .Include(w => w.Tags)
            .Include(w => w.Series)
            .Include(w => w.Urls)
            .Include(w => w.Dlcs)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (work is null)
            return false;

        var oldCoverKey = work.CoverKey;

        await ApplyAsync(work, request);

        work.UpdatedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync();
        await _cleaner.DeleteIfUnusedAsync(oldCoverKey, work.CoverKey);
        return true;
    }

    /// <summary>
    /// 軟刪除：標記刪除時間（同時更新修改時間），之後可以還原。找不到回傳 false。
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        var work = await _db.Works.FirstOrDefaultAsync(w => w.Id == id);
        if (work is null)
            return false;

        var time = _time.GetUtcNow().UtcDateTime;
        work.DeletedAt = time;
        work.UpdatedAt = time;

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>切換某一個網址的購買狀態，同時更新修改時間；作品或該位置的網址不存在時回傳 false。</summary>
    public async Task<bool> SetUrlPurchasedAsync(int id, int position, bool purchased)
    {
        var work = await _db.Works
            .Include(w => w.Urls)
            .FirstOrDefaultAsync(w => w.Id == id);

        // 作品不存在或沒有這個位置，兩種都是 null
        var url = work?.Urls.FirstOrDefault(u => u.Position == position);
        if (url is null)
            return false;

        url.Purchased = purchased;
        work!.UpdatedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync();
        return true;
    }
}
