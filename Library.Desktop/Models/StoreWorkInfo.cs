namespace Library.Desktop.Models;

/// <summary>
/// 從商店網址抓回來的作品資料，對應 GET /api/lookup 的 JSON。沒有的名稱是 ""，日期不確定時是 null。
/// </summary>
public record StoreWorkInfo(
    string TitleZh,
    string TitleJa,
    string TitleEn,
    string[] Authors,
    string[] Circles,
    DateOnly? ReleaseDate);
