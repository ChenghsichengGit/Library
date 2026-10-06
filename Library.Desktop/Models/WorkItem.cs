namespace Library.Desktop.Models;

/// <summary>
/// 前端的作品資料，對應 API 回傳的 JSON（後端的 WorkDto）。
/// </summary>
/// <remarks>
/// WPF 不引用後端的程式碼，只照著 JSON 的格式自己定義一個類別：雙方只約定 JSON 長什麼樣。
/// JSON 的 title 會自動對應到 Title（大小寫不用管）。
/// 只有寫在這裡的欄位會被讀取：後端加了欄位，這裡也要加，畫面才看得到。
/// </remarks>
public record WorkItem(
    int Id,
    string Title,
    string TitleZh,
    string TitleJa,
    string TitleEn,
    string Remark,
    int Score,
    bool Favorite,
    bool Purchased,
    DateOnly? ReleaseDate,
    DateTime CreatedAt);
