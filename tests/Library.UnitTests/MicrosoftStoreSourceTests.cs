using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Library.Application.Sources;
using Library.Infrastructure.Sources;

namespace Library.UnitTests;

/// <summary>
/// MicrosoftStoreSource 的單元測試：用假的 HttpMessageHandler 取代網路，不會真的連到 Microsoft Store。
/// </summary>
public class MicrosoftStoreSourceTests
{
    private class FakeHandler(Func<HttpRequestMessage, string> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(respond(request), Encoding.UTF8, "application/json")
            });
    }

    private const string Url = "https://www.xbox.com/zh-TW/games/store/hollow-knight-voidheart-edition/9MW9469V91LM";

    // 依照網址的 languages= 參數回傳不同語言的 JSON；requests 有給時記下送出的網址
    private static MicrosoftStoreSource CreateSource(Func<string, string> jsonForLanguage, List<Uri>? requests = null) =>
        new(new HttpClient(new FakeHandler(request =>
        {
            requests?.Add(request.RequestUri!);
            var language = Regex.Match(request.RequestUri!.Query, "[?&]languages=([^&]+)").Groups[1].Value;
            return jsonForLanguage(language);
        }))
        {
            BaseAddress = new Uri("https://displaycatalog.mp.microsoft.com/")
        });

    // 第一個方案不能購買（價格 0）、第二個可以購買：要挑到第二個；listPrice 是現價、msrp 是原價
    private const string AllImages = """
        { "ImagePurpose": "BoxArt", "Uri": "//store-images.s-microsoft.com/box.jpg" },
        { "ImagePurpose": "SuperHeroArt", "Uri": "//store-images.s-microsoft.com/hero.jpg" },
        { "ImagePurpose": "TitledHeroArt", "Uri": "//store-images.s-microsoft.com/titled.jpg" }
        """;

    private static string ProductJson(string title, string images = AllImages, string availabilities = """
        { "Actions": ["Details", "License"], "OrderManagementData": { "Price": { "ListPrice": 0, "MSRP": 0, "CurrencyCode": "TWD" } } },
        { "Actions": ["Details", "Purchase"], "OrderManagementData": { "Price": { "ListPrice": 259.5, "MSRP": 519, "CurrencyCode": "TWD" } } }
        """) => $$"""
        {
          "Products": [{
            "LocalizedProperties": [{
              "ProductTitle": "{{title}}",
              "DeveloperName": "Team Cherry",
              "PublisherName": "Cherry Publishing",
              "ShortDescription": " A classic adventure. ",
              "Images": [ {{images}} ]
            }],
            "MarketProperties": [{ "OriginalReleaseDate": "2018-09-25T17:00:00.0000000Z" }],
            "Properties": { "Categories": ["Action & adventure", "Platformer"] },
            "DisplaySkuAvailabilities": [{ "Availabilities": [ {{availabilities}} ] }]
          }]
        }
        """;

    // 防：名稱放錯語言、開發商與發行商拿反、封面挑到沒有標題的圖或少了 https:、簡介沒去空白、沒指定台灣區、Product ID 沒轉大寫
    [Fact]
    public async Task FetchAsync_ParsesAllFields()
    {
        var requests = new List<Uri>();
        var source = CreateSource(language => language switch
        {
            "zh-TW" => ProductJson("窟窿騎士"),
            "ja-JP" => ProductJson("ホロウナイト"),
            _ => ProductJson("Hollow Knight")
        }, requests);

        var result = await source.FetchAsync(new Uri("https://apps.microsoft.com/detail/9mw9469v91lm"));

        Assert.NotNull(result);
        Assert.Equal("窟窿騎士", result.TitleZh);
        Assert.Equal("ホロウナイト", result.TitleJa);
        Assert.Equal("Hollow Knight", result.TitleEn);
        Assert.Equal(["Team Cherry"], result.Authors);
        Assert.Equal(["Cherry Publishing"], result.Circles);
        Assert.Equal(new DateOnly(2018, 9, 25), result.ReleaseDate);
        Assert.Equal("https://store-images.s-microsoft.com/titled.jpg", result.CoverImageUrl);
        Assert.Equal(["Action & adventure", "Platformer"], result.Tags);
        Assert.Equal("A classic adventure.", result.Description);
        Assert.Equal("遊戲", result.WorkType);
        Assert.All(requests, uri =>
        {
            Assert.Contains("bigIds=9MW9469V91LM", uri.Query);
            Assert.Contains("market=TW", uri.Query);
        });
    }

    // 防：沒篩選可以購買的方案（拿到價格 0 的）、Microsoft 的 ListPrice（現價）和我們的 ListPrice（原價）搞反、折扣算錯
    [Fact]
    public async Task FetchAsync_Price_FromPurchasableAvailability()
    {
        var source = CreateSource(_ => ProductJson("Hollow Knight"));

        var result = await source.FetchAsync(new Uri(Url));

        Assert.Equal(259.5m, result!.Price);
        Assert.Equal(519m, result.ListPrice);
        Assert.Equal(50, result.Discount);
        Assert.Equal("TWD", result.Currency);
    }

    // 防：免費或沒有在賣的作品丟出例外，或價格變成 0
    [Fact]
    public async Task FetchAsync_NoPurchasableAvailability_NoPrice()
    {
        var source = CreateSource(_ => ProductJson("Free Game", availabilities: """
            { "Actions": ["Details", "Purchase"], "OrderManagementData": { "Price": { "ListPrice": 0, "MSRP": 0, "CurrencyCode": "TWD" } } }
            """));

        var result = await source.FetchAsync(new Uri(Url));

        Assert.Null(result!.Price);
        Assert.Null(result.ListPrice);
        Assert.Equal(0, result.Discount);
        Assert.Equal("", result.Currency);
    }

    // 防：沒有帶標題的圖時，封面變成 null 或挑到正方形的 BoxArt
    [Fact]
    public async Task FetchAsync_NoTitledHeroArt_FallsBackToSuperHeroArt()
    {
        var source = CreateSource(_ => ProductJson("Hollow Knight", images: """
            { "ImagePurpose": "BoxArt", "Uri": "//store-images.s-microsoft.com/box.jpg" },
            { "ImagePurpose": "SuperHeroArt", "Uri": "//store-images.s-microsoft.com/hero.jpg" }
            """));

        var result = await source.FetchAsync(new Uri(Url));

        Assert.Equal("https://store-images.s-microsoft.com/hero.jpg", result!.CoverImageUrl);
    }

    // 沒有翻譯時每個語言都回傳英文名稱，中文和日文應該是空的
    [Fact]
    public async Task FetchAsync_UntranslatedName_ReturnsEmpty()
    {
        var source = CreateSource(_ => ProductJson("Hollow Knight"));

        var result = await source.FetchAsync(new Uri(Url));

        Assert.Equal("", result!.TitleZh);
        Assert.Equal("", result.TitleJa);
    }

    [Fact]
    public async Task FetchAsync_NotFound_ReturnsNull()
    {
        var source = CreateSource(_ => """{ "Products": [] }""");

        Assert.Null(await source.FetchAsync(new Uri(Url)));
    }

    // 防：只認得其中一種網址、比對整個網址字串（網址裡有 xbox.com 的其他網站也通過）
    [Theory]
    [InlineData(Url, true)]
    [InlineData("https://www.microsoft.com/zh-tw/p/hollow-knight-voidheart-edition/9mw9469v91lm", true)]
    [InlineData("https://apps.microsoft.com/detail/9mw9469v91lm/", true)]
    [InlineData("https://www.xbox.com/zh-TW/games/all-games", false)]
    [InlineData("https://evil.com/www.xbox.com/9MW9469V91LM", false)]
    public void CanHandle_ChecksHostAndProductId(string url, bool expected)
    {
        Assert.Equal(expected, new MicrosoftStoreSource(new HttpClient()).CanHandle(new Uri(url)));
    }

    private static string BatchProduct(string id, decimal listPrice, decimal msrp) => $$"""
        {
          "ProductId": "{{id}}",
          "LocalizedProperties": [{ "ProductTitle": "{{id}}", "Images": [] }],
          "MarketProperties": [],
          "Properties": {},
          "DisplaySkuAvailabilities": [{ "Availabilities": [
            { "Actions": ["Purchase"], "OrderManagementData": { "Price": { "ListPrice": {{listPrice}}, "MSRP": {{msrp}}, "CurrencyCode": "TWD" } } }
          ] }]
        }
        """;

    // 防：免費作品被當成 0 元、key 沒用 ProductId（對不回網址）、一次只查一個
    [Fact]
    public async Task FetchPricesAsync_SkipsFreeProducts()
    {
        var requests = new List<Uri>();
        var source = CreateSource(_ => $$"""
            { "Products": [ {{BatchProduct("9MW9469V91LM", 259.5m, 519)}}, {{BatchProduct("9NBLGGH4R315", 0, 0)}} ] }
            """, requests);

        var prices = await source.FetchPricesAsync(["9MW9469V91LM", "9NBLGGH4R315", "9ZZZZZZZZZZZ"]);

        var price = Assert.Single(prices);
        Assert.Equal("9MW9469V91LM", price.Key);
        Assert.Equal(new StorePrice(259.5m, 519m, 50, "TWD"), price.Value);
        Assert.Contains("bigIds=9MW9469V91LM,9NBLGGH4R315,9ZZZZZZZZZZZ", Assert.Single(requests).Query);
    }

    // 防：Product ID 沒轉大寫（和批次結果的 key 對不上）
    [Fact]
    public void GetPriceKey_UpperCaseProductId()
    {
        var source = new MicrosoftStoreSource(new HttpClient());

        Assert.Equal("9MW9469V91LM", source.GetPriceKey(new Uri("https://apps.microsoft.com/detail/9mw9469v91lm")));
        Assert.Null(source.GetPriceKey(new Uri("https://store.steampowered.com/app/620")));
    }
}
