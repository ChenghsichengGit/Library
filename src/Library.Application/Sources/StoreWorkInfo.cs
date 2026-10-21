namespace Library.Application.Sources;

/// <summary>
/// 從商店抓回來的作品資料，各商店都轉成這個格式。只用來填表單，不直接存檔。
/// </summary>
/// <remarks>沒有的語言名稱、簡介是 ""，沒有標籤是空陣列；日期不確定、沒有封面時是 null。沒有價格時 Price 是 null、Currency 是 ""；原價（ListPrice）與折扣（Discount）在沒有特價時分別等於 Price 和 0；WorkType 是類型的名稱，目前一律是「遊戲」。</remarks>
public record StoreWorkInfo(
    string TitleZh,
    string TitleJa,
    string TitleEn,
    string[] Authors,
    string[] Circles,
    DateOnly? ReleaseDate,
    string? CoverImageUrl,
    string[] Tags,
    string Description,
    decimal? Price,
    decimal? ListPrice,
    int Discount,
    string Currency,
    string? WorkType);
