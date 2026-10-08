namespace Library.Application.Sources;

/// <summary>
/// 從外部商店抓回來的作品資料，各商店的格式都轉成這個統一的樣子。
/// </summary>
/// <remarks>
/// 只用來填 WPF 的表單，不直接存進資料庫：使用者確認、修改後按儲存，才走一般的新增流程。
/// 欄位名稱和 SaveWorkRequest 一致，前端可以直接對應。沒有的語言名稱是 ""，日期不確定時是 null。
/// </remarks>
public record StoreWorkInfo(
    string TitleZh,
    string TitleJa,
    string TitleEn,
    string[] Authors,
    string[] Circles,
    DateOnly? ReleaseDate);
