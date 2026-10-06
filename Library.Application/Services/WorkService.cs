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

    public WorkService(ILibraryDbContext db,  TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    /// <summary>
    /// 依條件查詢作品清單。query 裡沒給的條件就不篩選。
    /// </summary>
    public async Task<List<WorkDto>> GetWorksAsync(WorkQuery query)
    {
        // 這時還沒查資料庫，works 只是「查詢的描述」（IQueryable），下面一個條件一個條件疊上去
        var works = _db.Works.AsNoTracking();

        // Where 不會修改 works，而是回傳加了條件的新查詢，所以要存回 works
        // 不能用 w.Title：Title 是 C# 算出來的，資料庫沒有這個欄位，EF Core 翻譯不成 SQL
        if(!String.IsNullOrEmpty(query.Q))
            works = works.Where(w =>
                w.TitleZh.Contains(query.Q) ||
                w.TitleJa.Contains(query.Q) ||
                w.TitleEn.Contains(query.Q) ||
                w.Remark.Contains(query.Q));

        if(query.Favorite != null)
            works = works.Where(w => w.Favorite == query.Favorite);

        if(query.Purchased != null)
            works = works.Where(w => w.Purchased == query.Purchased);

        if(query.MinScore != null)
            works = works.Where(w => w.Score >= query.MinScore);

        if(query.MaxScore != null)
            works = works.Where(w => w.Score <= query.MaxScore);

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
        Apply(work, request);

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
        var work = await _db.Works.FirstOrDefaultAsync(w => w.Id == id);
        if (work is null)
            return false;

        // 不碰 CreatedAt：從資料庫讀出來時就是原本的建立時間，不動它就會保留
        Apply(work, request);

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
    /// 新增與修改共用：把 request 的內容填進 work（去除前後空白、沒填的給預設值）。
    /// 建立時間不在這裡設定：修改時不能動到它，由 CreateAsync 單獨設定。
    /// </summary>
    private static void Apply(Work work, SaveWorkRequest request)
    {
        // ?.Trim()：不是 null 才去空白；?? ""：是 null 就改成空字串（資料庫的名稱欄位不能是 NULL）
        work.TitleZh = request.TitleZh?.Trim() ?? "";
        work.TitleJa = request.TitleJa?.Trim() ?? "";
        work.TitleEn = request.TitleEn?.Trim() ?? "";
        work.Remark = request.Remark?.Trim() ?? "";
        work.ReleaseDate = request.ReleaseDate ?? null;
        work.Score = request.Score ?? 0;
        work.Favorite = request.Favorite ?? false;
        work.Purchased = request.Purchased ?? false;
    }
}
