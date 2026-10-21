using Library.Application.Dtos.Requests;
using Library.Application.Services;
using Library.Application.Sources;
using Library.IntegrationTests.Support;

namespace Library.IntegrationTests.Works;

/// <summary>
/// PriceUpdateService：依商店分組批次查詢價格，寫回每個網址。商店用假的 IPriceSource，不會連到網路。
/// </summary>
[Collection("SqlServer")]
public class WorkPriceUpdateTests(SqlServerFixture fixture) : WorkServiceTestBase(fixture)
{
    /// <summary>認得 https://{host}/{代碼} 的假商店；記下每一批查了哪些代碼，fail 為 true 時模擬連線失敗。</summary>
    private class FakePriceSource(string host, int batchSize, Dictionary<string, StorePrice> prices, bool fail = false)
        : IPriceSource
    {
        public List<string[]> Batches { get; } = [];

        public int BatchSize => batchSize;

        public string? GetPriceKey(Uri url) => url.Host == host ? url.AbsolutePath.Trim('/') : null;

        public Task<IReadOnlyDictionary<string, StorePrice>> FetchPricesAsync(
            IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
        {
            Batches.Add(keys.ToArray());
            if (fail)
                throw new HttpRequestException("連線失敗");
            return Task.FromResult<IReadOnlyDictionary<string, StorePrice>>(
                prices.Where(p => keys.Contains(p.Key)).ToDictionary());
        }
    }

    private static readonly StorePrice Sale = new(134, 268, 50, "TWD");

    private static SaveWorkRequest Work(string title, params string[] urls) => new()
    {
        TitleJa = title,
        WorkTypeId = 1,
        Urls = urls.Select(u => new SaveWorkUrlRequest { Url = u, Price = 999, Currency = "TWD" }).ToList()
    };

    private async Task<(int Updated, int NotUpdated)> RefreshAsync(params IPriceSource[] sources)
    {
        await using var db = CreateDbContext();
        var result = await new PriceUpdateService(db, sources).RefreshAsync();
        return (result.Updated, result.NotUpdated);
    }

    // 防：價格沒寫回或沒存檔、原價和折扣漏掉、其他網站的網址也被改、查不到價格的被清成 null
    [Fact]
    public async Task RefreshAsync_UpdatesKnownUrls_KeepsOthers()
    {
        var id = await CreateWorkAsync(Work("A", "https://shop.test/sale", "https://shop.test/free", "https://other.test/x"));
        var source = new FakePriceSource("shop.test", 50, new() { ["sale"] = Sale });

        Assert.Equal((1, 1), await RefreshAsync(source));

        var urls = (await GetWorkAsync(id))!.Urls;
        Assert.Equal((134m, 268m, 50), (urls[0].Price, urls[0].ListPrice, urls[0].Discount));
        // 免費或查不到的、其他網站的：維持原本的價格
        Assert.Equal(999m, urls[1].Price);
        Assert.Equal(999m, urls[2].Price);
    }

    // 防：沒依 BatchSize 分批、相同的代碼送了兩次、不同商店混在同一批
    [Fact]
    public async Task RefreshAsync_GroupsBySourceAndBatchSize()
    {
        await CreateWorksAsync(
            Work("A", "https://shop.test/1", "https://shop.test/2"),
            Work("B", "https://shop.test/3", "https://shop.test/1/"),
            Work("C", "https://ms.test/X"));
        var shop = new FakePriceSource("shop.test", 2, new() { ["1"] = Sale, ["2"] = Sale, ["3"] = Sale });
        var ms = new FakePriceSource("ms.test", 20, new() { ["X"] = Sale });

        Assert.Equal((5, 0), await RefreshAsync(shop, ms));

        Assert.All(shop.Batches, batch => Assert.True(batch.Length <= 2));
        Assert.Equal(["1", "2", "3"], shop.Batches.SelectMany(b => b).Distinct().Order());
        Assert.Equal([["X"]], ms.Batches);
    }

    // 防：一個商店連線失敗就整個中斷（其他商店也沒更新、已更新的沒存檔）
    [Fact]
    public async Task RefreshAsync_FailedSource_OthersStillUpdated()
    {
        var id = await CreateWorkAsync(Work("A", "https://down.test/1", "https://shop.test/1"));
        var down = new FakePriceSource("down.test", 50, [], fail: true);
        var shop = new FakePriceSource("shop.test", 50, new() { ["1"] = Sale });

        Assert.Equal((1, 1), await RefreshAsync(down, shop));

        Assert.Equal([999m, 134m], (await GetWorkAsync(id))!.Urls.Select(u => u.Price));
    }

    // 防：已刪除作品的網址也去查價格
    [Fact]
    public async Task RefreshAsync_DeletedWork_Skipped()
    {
        var id = await CreateWorkAsync(Work("A", "https://shop.test/1"));
        await using (var db = CreateDbContext())
            await CreateService(db).DeleteAsync(id);
        var source = new FakePriceSource("shop.test", 50, new() { ["1"] = Sale });

        Assert.Equal((0, 0), await RefreshAsync(source));
        Assert.Empty(source.Batches);
    }
}
