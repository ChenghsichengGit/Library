namespace Library.Desktop.Models;

/// <summary>
/// 從商店網址抓回來的作品資料，對應 API 的 GET /api/lookup 回傳的 JSON（後端的 StoreWorkInfo）。
/// </summary>
/// <remarks>
/// 和 WorkItem 一樣，WPF 不引用後端的程式碼，只照著 JSON 的格式自己定義。
/// 沒有的語言名稱是 ""，日期不確定時是 null；MainViewModel.FetchAsync 只用有值的欄位覆蓋表單。
/// </remarks>
public record StoreWorkInfo(
    string TitleZh,
    string TitleJa,
    string TitleEn,
    string[] Authors,
    string[] Circles,
    DateOnly? ReleaseDate);
