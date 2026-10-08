namespace Library.Application.Sources;

/// <summary>
/// 外部商店來源（Steam、DLsite…）的共同介面：給商店網址，回傳整理好的作品資料。
/// </summary>
/// <remarks>
/// 和硬體抽象層一樣：Application 只認識這個介面，實作放在 Infrastructure，每個商店一個類別。
/// 同一個介面可以在 DI 登記多個實作，注入 IEnumerable&lt;IStoreSource&gt; 時會一次拿到全部，
/// 新增商店時只要多寫一個實作並登記，呼叫的一方不用改。
/// </remarks>
public interface IStoreSource
{
    /// <summary>這個網址是不是由我處理。只看網址，不連網路。</summary>
    bool CanHandle(Uri url);

    /// <summary>
    /// 去商店抓資料並轉成 StoreWorkInfo。商店說找不到時回傳 null；連線失敗會丟出例外。
    /// 呼叫前應先確認 CanHandle 為 true。
    /// </summary>
    Task<StoreWorkInfo?> FetchAsync(Uri url, CancellationToken cancellationToken = default);
}
