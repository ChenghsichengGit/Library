namespace Library.Application.Sources;

/// <summary>商店回傳的價格。沒有特價時 ListPrice 等於 Price、Discount 是 0。</summary>
public record StorePrice(decimal Price, decimal ListPrice, int Discount, string Currency);

/// <summary>
/// 可以批次查詢價格的商店。和 IStoreSource 分開：抓作品資料要好幾個請求，更新價格只要一個請求查一批。
/// </summary>
public interface IPriceSource
{
    /// <summary>一次請求最多查幾個。</summary>
    int BatchSize { get; }

    /// <summary>網址對應的作品代碼（Steam 的 App ID、Microsoft Store 的 Product ID）；不是這個商店的網址回傳 null。</summary>
    string? GetPriceKey(Uri url);

    /// <summary>批次查詢價格。找不到或沒有價格（免費）的代碼不會出現在結果裡；連線失敗丟出例外。</summary>
    Task<IReadOnlyDictionary<string, StorePrice>> FetchPricesAsync(
        IReadOnlyList<string> keys, CancellationToken cancellationToken = default);
}
