using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Library.Application.Sources;

namespace Library.Infrastructure.Sources;

/// <summary>
/// Microsoft Store（Xbox／PC）的來源：從網址取出 Product ID，呼叫 displaycatalog API 抓回名稱、開發商、發行商、
/// 發售日、標籤、封面、簡介、價格；也可以一次查詢多個作品的價格。價格以台灣區的台幣計價。
/// </summary>
public partial class MicrosoftStoreSource : IStoreSource, IPriceSource
{
    // 價格用哪一區計價；台灣區是台幣
    private const string Market = "TW";

    private static readonly string[] Hosts = ["www.xbox.com", "www.microsoft.com", "apps.microsoft.com"];

    private readonly HttpClient _http;

    public MicrosoftStoreSource(HttpClient http) => _http = http;

    // 網址最後一段是 12 碼的 Product ID：…/9MW9469V91LM、…/9mw9469v91lm/
    [GeneratedRegex(@"/([0-9A-Za-z]{12})/?$")]
    private static partial Regex ProductIdPattern();

    // ── 抓取作品資料（IStoreSource） ──

    /// <summary>網域是 xbox.com、microsoft.com 或 apps.microsoft.com，而且最後一段是 Product ID 才處理。</summary>
    public bool CanHandle(Uri url) =>
        Hosts.Contains(url.Host) && ProductIdPattern().IsMatch(url.AbsolutePath);

    /// <summary>抓取作品資料，找不到時回傳 null。名稱依語言各查一次；其他資料取英文版。</summary>
    public async Task<StoreWorkInfo?> FetchAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var id = ProductId(url);

        var zhTask = GetProductAsync(id, "zh-TW", cancellationToken);
        var jaTask = GetProductAsync(id, "ja-JP", cancellationToken);
        var enTask = GetProductAsync(id, "en-US", cancellationToken);
        await Task.WhenAll(zhTask, jaTask, enTask);

        var en = enTask.Result;
        if (en is null)
            return null;

        var info = en.LocalizedProperties[0];
        var price = FindPrice(en) is { } p ? ToStorePrice(p) : null;

        return new StoreWorkInfo(
            TitleZh: Translated(zhTask.Result, info.ProductTitle),
            TitleJa: Translated(jaTask.Result, info.ProductTitle),
            TitleEn: info.ProductTitle,
            Authors: NonEmpty(info.DeveloperName),
            Circles: NonEmpty(info.PublisherName),
            ReleaseDate: en.MarketProperties.FirstOrDefault()?.OriginalReleaseDate is { } date
                ? DateOnly.FromDateTime(date)
                : null,
            CoverImageUrl: FindCover(info),
            Tags: en.Properties.Categories ?? [],
            Description: info.ShortDescription?.Trim() ?? "",
            Price: price?.Price,
            ListPrice: price?.ListPrice,
            Discount: price?.Discount ?? 0,
            Currency: price?.Currency ?? "",
            WorkType: "遊戲");
    }

    // ── 批次查詢價格（IPriceSource） ──

    /// <summary>一次最多查 20 個 Product ID。</summary>
    public int BatchSize => 20;

    /// <summary>Microsoft Store 的網址回傳 Product ID（大寫），其他網址回傳 null。</summary>
    public string? GetPriceKey(Uri url) => CanHandle(url) ? ProductId(url) : null;

    /// <summary>一次查詢多個作品的價格；免費、沒有在賣或查不到的不會出現在結果裡。</summary>
    public async Task<IReadOnlyDictionary<string, StorePrice>> FetchPricesAsync(
        IReadOnlyList<string> keys, CancellationToken cancellationToken = default)
    {
        var response = await _http.GetFromJsonAsync<MsResponse>(
            $"v7.0/products?bigIds={string.Join(',', keys)}&market={Market}&languages=en-US", cancellationToken);

        var prices = new Dictionary<string, StorePrice>();
        foreach (var product in response?.Products ?? [])
        {
            if (FindPrice(product) is { } price)
                prices[product.ProductId] = ToStorePrice(price);
        }

        return prices;
    }

    // ── 共用 ──

    // API 只認大寫
    private static string ProductId(Uri url) =>
        ProductIdPattern().Match(url.AbsolutePath).Groups[1].Value.ToUpperInvariant();

    private async Task<MsProduct?> GetProductAsync(string id, string language, CancellationToken cancellationToken)
    {
        var response = await _http.GetFromJsonAsync<MsResponse>(
            $"v7.0/products?bigIds={id}&market={Market}&languages={language}", cancellationToken);
        // 找不到的 Product ID 會回傳空陣列
        return response?.Products.FirstOrDefault();
    }

    // 沒有這個語言的名稱時會回傳英文名稱，和英文一樣就當作沒有翻譯（同 SteamSource）
    private static string Translated(MsProduct? product, string english) =>
        product?.LocalizedProperties.FirstOrDefault()?.ProductTitle is { } title && title != english ? title : "";

    private static string[] NonEmpty(string? name) => string.IsNullOrWhiteSpace(name) ? [] : [name.Trim()];

    // 橫向的大圖，比例和 Steam 的封面相近；網址可能是 // 開頭
    private static string? FindCover(MsLocalized info)
    {
        var image = info.Images.FirstOrDefault(i => i.ImagePurpose == "TitledHeroArt")
                    ?? info.Images.FirstOrDefault(i => i.ImagePurpose == "SuperHeroArt");
        return image?.Uri is { } uri && uri.StartsWith("//") ? "https:" + uri : image?.Uri;
    }

    // 第一個可以購買、而且有價格的方案；免費或沒有在賣時是 null
    private static MsPrice? FindPrice(MsProduct product) =>
        product.DisplaySkuAvailabilities
            .SelectMany(s => s.Availabilities)
            .Where(a => a.Actions.Contains("Purchase"))
            .Select(a => a.OrderManagementData?.Price)
            .FirstOrDefault(p => p is not null && p.MSRP > 0);

    // Microsoft 的 ListPrice 是現價、MSRP 是原價，和我們的命名相反
    private static StorePrice ToStorePrice(MsPrice price) => new(
        price.ListPrice,
        price.MSRP,
        price.MSRP > price.ListPrice ? (int)Math.Round((1 - price.ListPrice / price.MSRP) * 100) : 0,
        price.CurrencyCode);

    // API 回應的形狀，只宣告需要的欄位（PascalCase，和 C# 的命名相同，不用另外設定）
    private record MsResponse(MsProduct[] Products);

    private record MsProduct(
        string ProductId,
        MsLocalized[] LocalizedProperties,
        MsMarket[] MarketProperties,
        MsProperties Properties,
        MsSkuAvailability[] DisplaySkuAvailabilities);

    private record MsLocalized(
        string ProductTitle,
        string? DeveloperName,
        string? PublisherName,
        string? ShortDescription,
        MsImage[] Images);

    private record MsImage(string ImagePurpose, string Uri);

    private record MsMarket(DateTime? OriginalReleaseDate);

    private record MsProperties(string[]? Categories);

    private record MsSkuAvailability(MsAvailability[] Availabilities);

    private record MsAvailability(string[] Actions, MsOrderData? OrderManagementData);

    private record MsOrderData(MsPrice? Price);

    private record MsPrice(decimal ListPrice, decimal MSRP, string CurrencyCode);
}
