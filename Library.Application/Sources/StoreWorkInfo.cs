namespace Library.Application.Sources;

/// <summary>
/// 從商店抓回來的作品資料，各商店都轉成這個格式。只用來填表單，不直接存檔。
/// </summary>
/// <remarks>沒有的語言名稱是 ""，日期不確定時是 null。</remarks>
public record StoreWorkInfo(
    string TitleZh,
    string TitleJa,
    string TitleEn,
    string[] Authors,
    string[] Circles,
    DateOnly? ReleaseDate);
