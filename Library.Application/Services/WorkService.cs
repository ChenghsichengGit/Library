using Library.Application.Abstractions;
using Library.Application.Dtos;
using Library.Application.Exceptions;
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
            .AsNoTracking();

        // 比對三種語言的名稱而不是 Title，用任一語言都搜得到
        if (!String.IsNullOrEmpty(query.Q))
            works = works.Where(w =>
                w.TitleZh.Contains(query.Q) ||
                w.TitleJa.Contains(query.Q) ||
                w.TitleEn.Contains(query.Q) ||
                w.Remark.Contains(query.Q) ||
                w.Creators.Any(wc => wc.Creator.Name.Contains(query.Q)));

        if (query.Favorite != null)
            works = works.Where(w => w.Favorite == query.Favorite);

        if (query.Purchased != null)
            works = works.Where(w => w.Purchased == query.Purchased);

        if (query.WorkTypeId != null)
            works = works.Where(w => w.WorkTypeId == query.WorkTypeId);

        if (query.MinScore != null)
            works = works.Where(w => w.Score >= query.MinScore);

        if (query.MaxScore != null)
            works = works.Where(w => w.Score <= query.MaxScore);

        var ordered = query.Sort switch
        {
            WorkSort.Title => query.Desc
                ? works.OrderByDescending(w => w.Title)
                : works.OrderBy(w => w.Title),

            // 沒有上架日的不管升降冪都排最後：先依「是不是 null」排，升降冪只套用在日期上
            WorkSort.ReleaseDate => query.Desc
                ? works.OrderBy(w => w.ReleaseDate == null).ThenByDescending(w => w.ReleaseDate)
                : works.OrderBy(w => w.ReleaseDate == null).ThenBy(w => w.ReleaseDate),

            WorkSort.Score => query.Desc
                ? works.OrderByDescending(w => w.Score)
                : works.OrderBy(w => w.Score),

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

        work.CreatedAt = _time.GetUtcNow().UtcDateTime;

        _db.Works.Add(work);
        await _db.SaveChangesAsync();

        return WorkDto.From(work);
    }

    /// <summary>
    /// 用 request 整筆取代作品內容（PUT），找不到回傳 false。類型不存在時丟出 WorkTypeNotFoundException。
    /// </summary>
    public async Task<bool> UpdateAsync(int id, SaveWorkRequest request)
    {
        // Include 舊的關聯，ApplyAsync 的 Clear() 才刪得掉
        var work = await _db.Works
            .Include(w => w.Creators)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (work is null)
            return false;

        await ApplyAsync(work, request);

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 軟刪除：只標記刪除時間，之後可以還原。找不到回傳 false。
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        var work = await _db.Works.FirstOrDefaultAsync(w => w.Id == id);
        if (work is null)
            return false;
        work.DeletedAt = _time.GetUtcNow().UtcDateTime;

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 新增與修改共用：把 request 填進 work，並把作者、社團整份換成 request 的名單（名字已存在就沿用）。
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
        work.Purchased = request.Purchased ?? false;

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
