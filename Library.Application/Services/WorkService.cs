using Library.Application.Abstractions;
using Library.Application.Dtos;
using Library.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

/// <summary>
/// 作品的業務規則：查詢、新增、修改、軟刪除。
/// 由 WorksController 呼叫；只透過 ILibraryDbContext 存取資料，不知道背後是 SQL Server。
/// 由 DI 容器建立（見 Application/DependencyInjection.cs），每個 HTTP 請求一個實例。
/// </summary>
public class WorkService
{
    private readonly ILibraryDbContext _db;

    // 「現在時間」也透過注入取得，測試時才能換成時間固定的假時鐘
    private readonly TimeProvider _time;

    public WorkService(ILibraryDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    /// <summary>
    /// 依條件查詢作品清單，並依 query.Sort 排序。query 裡沒給的篩選條件就不篩選。
    /// </summary>
    public async Task<List<WorkDto>> GetWorksAsync(WorkQuery query)
    {
        // 這時還沒查資料庫，works 只是「查詢的描述」（IQueryable），下面一個條件一個條件疊上去
        // Include 兩層：WorkDto.From 要讀作者名字，從 Work 經過 WorkCreator 走到 Creator
        var works = _db.Works
            .Include(w => w.Creators)
            .ThenInclude(wc => wc.Creator)
            .AsNoTracking();

        // Where 不會修改 works，而是回傳加了條件的新查詢，所以要存回 works
        // 比對三種語言的名稱而不是 Title：Title 只是其中一個，用其他語言的名稱搜尋也要找得到
        if (!String.IsNullOrEmpty(query.Q))
            works = works.Where(w =>
                w.TitleZh.Contains(query.Q) ||
                w.TitleJa.Contains(query.Q) ||
                w.TitleEn.Contains(query.Q) ||
                w.Remark.Contains(query.Q) ||
                // 翻譯成 SQL 的 EXISTS：這部作品有任何一個作者或社團的名字符合
                w.Creators.Any(wc => wc.Creator.Name.Contains(query.Q)));

        if (query.Favorite != null)
            works = works.Where(w => w.Favorite == query.Favorite);

        if (query.Purchased != null)
            works = works.Where(w => w.Purchased == query.Purchased);

        if (query.MinScore != null)
            works = works.Where(w => w.Score >= query.MinScore);

        if (query.MaxScore != null)
            works = works.Where(w => w.Score <= query.MaxScore);

        // 先篩選、再排序（和 SQL 的 WHERE … ORDER BY 順序一致）
        // ordered 是 IOrderedQueryable：已經排過序的查詢，後面才能接 ThenBy
        var ordered = query.Sort switch
        {
            WorkSort.Title => query.Desc
                ? works.OrderByDescending(w => w.Title)
                : works.OrderBy(w => w.Title),

            // 沒有上架日的不管升降冪都排最後：先依「是不是 null」排（false 在前），這個方向永遠不變；
            // 使用者選的升降冪只套用在日期上。SQL Server 預設把 NULL 當最小值，升冪時會排在最前面
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

        // 排序值相同時（例如評分一樣），資料庫回傳的順序不保證固定；最後依 Id 排，順序才完全確定，之後分頁也不會重複或漏掉
        works = ordered.ThenBy(w => w.Id);

        // 到這裡才真的去資料庫，所有條件合成一句 SQL
        var list = await works.ToListAsync();

        // 每個 Work（資料庫的格式）轉成 WorkDto（API 對外的格式）
        return list.Select(WorkDto.From).ToList();
    }

    /// <summary>
    /// 查詢單筆作品。找不到回傳 null（Controller 會轉成 404）。
    /// </summary>
    public async Task<WorkDto?> GetByIdAsync(int id)
    {
        // AsNoTracking：只讀不改，EF Core 不用記住這筆資料，比較省資源
        var work = await _db.Works
            .Include(w => w.Creators)
            .ThenInclude(wc => wc.Creator)
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == id);

        if (work is null)
            return null;

        return WorkDto.From(work);
    }

    /// <summary>
    /// 新增作品，回傳建立好的作品（包含資料庫產生的 Id）。
    /// </summary>
    public async Task<WorkDto> CreateAsync(SaveWorkRequest request)
    {
        var work = new Work();
        await ApplyAsync(work, request);

        // 建立時間由系統決定，不讓使用者自己填；一律存 UTC
        work.CreatedAt = _time.GetUtcNow().UtcDateTime;

        _db.Works.Add(work);

        // 這時才真的寫進資料庫；寫入後 work.Id 會自動填上新的編號
        await _db.SaveChangesAsync();

        return WorkDto.From(work);
    }

    /// <summary>
    /// 修改作品：用 request 的內容整筆取代（PUT）。找不到回傳 false（Controller 會轉成 404）。
    /// </summary>
    public async Task<bool> UpdateAsync(int id, SaveWorkRequest request)
    {
        // 要修改所以不加 AsNoTracking：EF Core 會記住原本的值，存檔時只 UPDATE 有變的欄位
        // Include 舊的關聯，ApplyAsync 的 Clear() 才刪得掉；不用 ThenInclude，刪除只需要 WorkCreator 自己的主鍵
        var work = await _db.Works
            .Include(w => w.Creators)
            .FirstOrDefaultAsync(w => w.Id == id);
        if (work is null)
            return false;

        // 不碰 CreatedAt：從資料庫讀出來時就是原本的建立時間，不動它就會保留
        await ApplyAsync(work, request);

        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 軟刪除：不真的刪掉資料，只標記刪除時間，之後可以還原。
    /// 已刪除的作品會被全域查詢過濾器自動排除（見 LibraryDbContext）。找不到回傳 false。
    /// </summary>
    public async Task<bool> DeleteAsync(int id)
    {
        var work = await _db.Works.FirstOrDefaultAsync(w => w.Id == id);
        if (work is null)
            return false;
        work.DeletedAt = _time.GetUtcNow().UtcDateTime;

        // 少了這行，修改只存在記憶體裡，請求結束就消失了
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// 新增與修改共用：把 request 的內容填進 work（去除前後空白、沒填的給預設值），
    /// 並把作者、社團名單整份換成 request 的內容（名字已存在就沿用，不存在就建立）。
    /// 建立時間不在這裡設定：修改時不能動到它，由 CreateAsync 單獨設定。
    /// </summary>
    /// <remarks>
    /// 只改記憶體裡的 work，不存檔；由呼叫的 CreateAsync／UpdateAsync 執行 SaveChangesAsync，所有變更在同一個交易裡。
    /// 修改時 work.Creators 必須已經 Include，否則舊的關聯清不掉。
    /// </remarks>
    private async Task ApplyAsync(Work work, SaveWorkRequest request)
    {
        // ?.Trim()：不是 null 才去空白；?? ""：是 null 就改成空字串（資料庫的名稱欄位不能是 NULL）
        work.TitleZh = request.TitleZh?.Trim() ?? "";
        work.TitleJa = request.TitleJa?.Trim() ?? "";
        work.TitleEn = request.TitleEn?.Trim() ?? "";
        work.Remark = request.Remark?.Trim() ?? "";
        work.ReleaseDate = request.ReleaseDate;
        work.Score = request.Score ?? 0;
        work.Favorite = request.Favorite ?? false;
        work.Purchased = request.Purchased ?? false;

        var authors = CleanNames(request.Authors);
        var circles = CleanNames(request.Circles);

        // 作者和社團的名字合在一起，一次查完（WHERE Name IN (...)），不要一個名字查一次
        // Dictionary 要不分大小寫：SQL Server 用 valve 會查回 Valve，C# 這邊的比對規則要和它一致，
        // 否則會找不到而建立新的 valve，存檔時撞上唯一索引
        var allNames = authors.Concat(circles).ToList();
        var creators = await _db.Creators
            .Where(c => allNames.Contains(c.Name))
            .ToDictionaryAsync(c => c.Name, StringComparer.OrdinalIgnoreCase);

        // 清掉舊的關聯再加回新的；EF Core 存檔時比對差異，只刪除或新增有變的那幾筆
        work.Creators.Clear();
        foreach (var name in authors)
            work.Creators.Add(new WorkCreator { Creator = GetOrCreate(name), Role = CreatorRole.Author });
        foreach (var name in circles)
            work.Creators.Add(new WorkCreator { Creator = GetOrCreate(name), Role = CreatorRole.Circle });

        // 新建的 Creator 要放回 Dictionary：作者和社團都是同一個新名字時，第二次才會拿到同一個物件，而不是建立兩個
        // 新的 Creator 不用另外 Add 到 _db.Creators，存檔時 EF Core 會沿著 work → WorkCreator → Creator 找到它
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
    /// 整理前端送來的名字清單：null 當成空清單、濾掉 null 和空白、去掉前後空白、去掉重複（不分大小寫，保留先出現的寫法）。
    /// </summary>
    /// <remarks>順序有意義：先濾掉 null 才能 Trim，先 Trim 才能正確判斷重複。</remarks>
    private static List<string> CleanNames(List<string>? names) =>
        (names ?? [])
        .Where(n => !string.IsNullOrWhiteSpace(n))
        .Select(n => n.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
}