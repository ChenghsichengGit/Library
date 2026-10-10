namespace Library.Application.Sources;

/// <summary>
/// 依網址挑出負責的商店來源並查詢作品資料。只認識 IStoreSource，不知道有哪些商店。
/// </summary>
public class StoreLookupService(IEnumerable<IStoreSource> sources)
{
    /// <summary>有沒有來源支援這個網址。</summary>
    public bool CanLookup(Uri url) => sources.Any(s => s.CanHandle(url));

    /// <summary>
    /// 交給第一個支援這個網址的來源抓取。找不到作品回傳 null。呼叫前要先確認 CanLookup。
    /// </summary>
    public async Task<StoreWorkInfo?> LookupAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var source = sources.First(s => s.CanHandle(url));
        return await source.FetchAsync(url, cancellationToken);
    }
}
