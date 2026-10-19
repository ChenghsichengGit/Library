namespace Library.Application.Sources;

/// <summary>
/// 外部商店來源（Steam、DLsite…）的共同介面：給商店網址，回傳整理好的作品資料。
/// 實作放在 Infrastructure，新增商店時只要多寫一個實作並登記。
/// </summary>
public interface IStoreSource
{
    /// <summary>這個網址是不是由我處理。只看網址，不連網路。</summary>
    bool CanHandle(Uri url);

    /// <summary>
    /// 抓取作品資料。商店說找不到時回傳 null；連線失敗丟出例外。呼叫前應先確認 CanHandle。
    /// </summary>
    Task<StoreWorkInfo?> FetchAsync(Uri url, CancellationToken cancellationToken = default);
}
