namespace Library.Application.Sources;

/// <summary>
/// 依網址挑出負責的商店來源並查詢作品資料。由 LookupController 呼叫。
/// </summary>
/// <remarks>
/// sources 是 DI 裡所有登記成 IStoreSource 的實作（目前只有 SteamSource）。
/// 這個類別只認識介面，不知道有哪些商店：新增 DLsite 時只要多登記一個實作，這裡不用改。
/// </remarks>
public class StoreLookupService(IEnumerable<IStoreSource> sources)
{
    /// <summary>有沒有任何來源支援這個網址。只看網址，不連網路。</summary>
    public bool CanLookup(Uri url) => sources.Any(s => s.CanHandle(url));

    /// <summary>
    /// 交給第一個支援這個網址的來源抓取。商店說找不到時回傳 null；連線失敗的例外會往上丟給 Controller。
    /// 呼叫前要先確認 CanLookup 為 true，否則 First 找不到會丟例外。
    /// </summary>
    public async Task<StoreWorkInfo?> LookupAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var source = sources.First(s => s.CanHandle(url));
        return await source.FetchAsync(url, cancellationToken);
    }
}
