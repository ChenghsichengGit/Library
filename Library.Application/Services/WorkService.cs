using Library.Application.Abstractions;
using Library.Application.Dtos;
using Library.Application.Exceptions;
using Library.Application.Urls;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

/// <summary>
/// 作品的業務規則：查詢、新增、修改、軟刪除。由 WorksController 呼叫。
/// </summary>
public class WorkService
{
    private readonly ILibraryDbContext _db;

    // 透過注入取得現在時間，測試才能換成固定的假時鐘
    private readonly TimeProvider _time;

    public WorkService(ILibraryDbContext db, TimeProvider time)
    {
        _db = db;
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
                w.Remark.Contains(query.Q) ||
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

        if (query.MinScore != null)
            works = works.Where(w => w.Score >= query.MinScore);

        if (query.MaxScore != null)
            works = works.Where(w => w.Score <= query.MaxScore);

        if (query.DlcUnbought)
            works = works.Where(w => w.Dlcs.Any(d => !d.Purchased));

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
        return list.Select(WorkDto.From).ToList();
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

        return WorkDto.From(work);
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

        return WorkDto.From(work);
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

        await ApplyAsync(work, request);

        work.UpdatedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync();
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

    /// <summary>
    /// 新增與修改共用：把 request 填進 work，並把作者、社團、標籤、網址整份換成 request 的內容、設定系列（名字已存在就沿用）。
    /// 只改記憶體，由呼叫端存檔；不設定建立時間。
    /// </summary>
    private async Task ApplyAsync(Work work, SaveWorkRequest request)
    {
        work.TitleZh = request.TitleZh?.Trim() ?? "";
        work.TitleJa = request.TitleJa?.Trim() ?? "";
        work.TitleEn = request.TitleEn?.Trim() ?? "";
        work.Remark = request.Remark?.Trim() ?? "";
        work.ReleaseDate = request.ReleaseDate;
        work.Score = request.Score ?? 0;
        work.Favorite = request.Favorite ?? false;

        // [Required] 已擋掉 null。先查類型再處理作者，不存在就不必多查一次資料庫
        var typeId = request.WorkTypeId!.Value;
        var type = await _db.WorkTypes.FindAsync(typeId);
        if (type is null)
            throw new WorkTypeNotFoundException(typeId);
        // 設定物件而不是 Id：CreateAsync 最後的 WorkDto.From 要讀類型名稱
        work.WorkType = type;

        var authors = CleanNames(request.Authors);
        var circles = CleanNames(request.Circles);

        // 名字一次查完；Dictionary 不分大小寫，和 SQL Server 的比對規則一致，否則會重複建立而撞上唯一索引
        var allNames = authors.Concat(circles).ToList();
        var creators = await _db.Creators
            .Where(c => allNames.Contains(c.Name))
            .ToDictionaryAsync(c => c.Name, StringComparer.OrdinalIgnoreCase);

        work.Creators.Clear();
        foreach (var name in authors)
            work.Creators.Add(new WorkCreator { Creator = GetOrCreate(name), Role = CreatorRole.Author });
        foreach (var name in circles)
            work.Creators.Add(new WorkCreator { Creator = GetOrCreate(name), Role = CreatorRole.Circle });

        // 標籤只有一份名單且已去重複，新建的不必放回 Dictionary；WorkTags 由 EF 寫入
        var tagNames = CleanNames(request.Tags);
        var tags = await _db.Tags
            .Where(t => tagNames.Contains(t.Name))
            .ToDictionaryAsync(t => t.Name, StringComparer.OrdinalIgnoreCase);

        work.Tags.Clear();
        foreach (var name in tagNames)
        {
            if (!tags.TryGetValue(name, out var tag))
                tag = new Tag { Name = name };
            work.Tags.Add(tag);
        }

        // 單一名稱直接在資料庫比對（不分大小寫），不需要 Dictionary
        var seriesName = request.Series?.Trim();
        if (string.IsNullOrEmpty(seriesName))
        {
            work.Series = null;
        }
        else
        {
            work.Series = await _db.Series.FirstOrDefaultAsync(s => s.Name == seriesName)
                          ?? new Series { Name = seriesName };
        }

        // 空白列略過；正規化後相同的只留第一個。Position 用整理後的索引
        var urls = (request.Urls ?? [])
            .Where(u => !string.IsNullOrWhiteSpace(u.Url))
            .DistinctBy(u => UrlNormalizer.Normalize(u.Url))
            .ToList();

        // 外鍵必填，移出清單的網址由 EF 刪除（孤兒刪除）
        work.Urls.Clear();
        for (var i = 0; i < urls.Count; i++)
        {
            work.Urls.Add(new WorkUrl
            {
                Position = i,
                Url = urls[i].Url!.Trim(),
                Purchased = urls[i].Purchased,
                Language = urls[i].Language,
                Price = urls[i].Price,
                Currency = urls[i].Currency?.Trim().ToUpperInvariant() ?? ""
            });
        }

        // DLC 以名稱為準，空白名稱的列略過；不去除重複：沒有網址的 DLC 正規化後都是 null，會被當成同一個
        var dlcs = (request.Dlcs ?? [])
            .Where(d => !string.IsNullOrWhiteSpace(d.Name))
            .ToList();

        work.Dlcs.Clear();
        for (var i = 0; i < dlcs.Count; i++)
        {
            work.Dlcs.Add(new WorkDlc
            {
                Position = i,
                Name = dlcs[i].Name!.Trim(),
                Url = dlcs[i].Url?.Trim() ?? "",
                Purchased = dlcs[i].Purchased,
                Price = dlcs[i].Price,
                Currency = dlcs[i].Currency?.Trim().ToUpperInvariant() ?? ""
            });
        }

        // 新建的放回 Dictionary：作者和社團是同一個新名字時，才不會建立兩次
        Creator GetOrCreate(string name)
        {
            if (!creators.TryGetValue(name, out var creator))
            {
                creator = new Creator { Name = name };
                creators[name] = creator;
            }

            return creator;
        }
    }

    /// <summary>
    /// 整理名字清單：濾掉 null 和空白、去前後空白、不分大小寫去重複（保留先出現的寫法）。
    /// </summary>
    private static List<string> CleanNames(List<string>? names) =>
        (names ?? [])
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Select(n => n.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}
