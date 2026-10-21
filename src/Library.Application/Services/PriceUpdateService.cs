using Library.Application.Abstractions;
using Library.Application.Dtos.Responses;
using Library.Application.Sources;
using Microsoft.EntityFrameworkCore;

namespace Library.Application.Services;

/// <summary>
/// 更新所有未刪除作品的商店網址價格：依商店分組，每一批只送一次請求。
/// </summary>
public class PriceUpdateService(ILibraryDbContext db, IEnumerable<IPriceSource> sources)
{
    // 每一批之間的間隔，避免短時間內送出太多請求而被商店擋下
    private static readonly TimeSpan Gap = TimeSpan.FromSeconds(1);

    public async Task<PriceUpdateResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        // 要修改並存檔，所以不加 AsNoTracking；全域篩選會排除已刪除作品的網址
        var urls = await db.Works.SelectMany(w => w.Urls).ToListAsync(cancellationToken);

        // 每個網址找出負責的商店和作品代碼；手動填的、其他網站的網址不處理
        var targets = urls
            .Select(url => (Url: url, Found: FindSource(url.Url)))
            .Where(t => t.Found is not null)
            .ToList();

        int updated = 0, notUpdated = 0;
        var isFirstBatch = true;
        foreach (var group in targets.GroupBy(t => t.Found!.Value.Source))
        {
            foreach (var batch in group.Chunk(group.Key.BatchSize))
            {
                if (!isFirstBatch)
                    await Task.Delay(Gap, cancellationToken);
                isFirstBatch = false;

                // 同一部作品可能有兩個相同的網址，代碼去重複後再送
                var keys = batch.Select(t => t.Found!.Value.Key).Distinct().ToList();

                IReadOnlyDictionary<string, StorePrice> prices;
                try
                {
                    prices = await group.Key.FetchPricesAsync(keys, cancellationToken);
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                           && !cancellationToken.IsCancellationRequested)
                {
                    // 這一批連線失敗（或逾時）不影響其他批
                    notUpdated += batch.Length;
                    continue;
                }

                foreach (var (url, found) in batch)
                {
                    if (prices.TryGetValue(found!.Value.Key, out var price))
                    {
                        url.Price = price.Price;
                        url.ListPrice = price.ListPrice;
                        url.Discount = price.Discount;
                        url.Currency = price.Currency;
                        updated++;
                    }
                    else
                    {
                        notUpdated++;
                    }
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new PriceUpdateResult(updated, notUpdated);
    }

    // 第一個認得這個網址的商店；網址格式不對或沒有人認得時是 null
    private (IPriceSource Source, string Key)? FindSource(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        foreach (var source in sources)
        {
            if (source.GetPriceKey(uri) is { } key)
                return (source, key);
        }

        return null;
    }
}
