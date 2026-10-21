using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Library.Infrastructure.Sources;

namespace Library.UnitTests;

/// <summary>
/// SteamSource 的單元測試：用假的 HttpMessageHandler 取代網路，不會真的連到 Steam。
/// </summary>
/// <remarks>
/// JSON 的欄位名稱對不上時不會報錯，只會得到預設值，所以每個欄位都要斷言。
/// </remarks>
public class SteamSourceTests
{
    private class FakeHandler(Func<HttpRequestMessage, string> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = respond(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    // 依照網址的 l= 參數回傳不同語言的 JSON，模擬 Steam 的行為；requests 有給時記下送出的網址
    private static SteamSource CreateSource(Func<string, string> jsonForLanguage, List<Uri>? requests = null) =>
        new(new HttpClient(new FakeHandler(request =>
        {
            requests?.Add(request.RequestUri!);
            var language = Regex.Match(request.RequestUri!.Query, "[?&]l=([^&]+)").Groups[1].Value;
            return jsonForLanguage(language);
        }))
        {
            BaseAddress = new Uri("https://store.steampowered.com/")
        });

    private const string HeaderImage = "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/620/header.jpg";

    // developers 和 publishers 刻意不同，作者和社團拿反時才抓得到；headerImage 是 null 時不放這個欄位
    // extra 是額外的欄位（genres、short_description），沒給就是 Steam 沒有這兩個欄位的情況
    private static string AppJson(string name, string date = "18 Apr, 2011", string? headerImage = HeaderImage,
        string extra = "") => $$"""
        {
          "620": {
            "success": true,
            "data": {
              "name": "{{name}}",
              {{(headerImage is null ? "" : $"\"header_image\": \"{headerImage}\",")}}
              {{extra}}
              "developers": ["Valve"],
              "publishers": ["Valve", "EA"],
              "release_date": { "coming_soon": false, "date": "{{date}}" }
            }
          }
        }
        """;

    // 三個語言的名稱都不同，放錯欄位時才抓得到
    [Fact]
    public async Task FetchAsync_ParsesAllFields()
    {
        var source = CreateSource(language => language switch
        {
            "tchinese" => AppJson("傳送門 2"),
            "japanese" => AppJson("ポータル2"),
            _ => AppJson("Portal 2")
        });

        var result = await source.FetchAsync(new Uri("https://store.steampowered.com/app/620/Portal_2/"));

        Assert.NotNull(result);
        Assert.Equal("傳送門 2", result.TitleZh);
        Assert.Equal("ポータル2", result.TitleJa);
        Assert.Equal("Portal 2", result.TitleEn);
        Assert.Equal(["Valve"], result.Authors);
        Assert.Equal(["Valve", "EA"], result.Circles);
        Assert.Equal(new DateOnly(2011, 4, 18), result.ReleaseDate);
        Assert.Equal(HeaderImage, result.CoverImageUrl);
        Assert.Equal("遊戲", result.WorkType);
    }

    [Fact]
    public async Task FetchAsync_NoHeaderImage_ReturnsNullCover()
    {
        var source = CreateSource(_ => AppJson("Portal 2", headerImage: null));

        var result = await source.FetchAsync(new Uri("https://store.steampowered.com/app/620"));

        Assert.Null(result!.CoverImageUrl);
    }

    [Theory]
    [InlineData("https://store.steampowered.com/app/620/Portal_2/", true)]
    [InlineData("https://store.steampowered.com/app/620", true)]
    [InlineData("https://store.steampowered.com/search/?term=portal", false)]
    [InlineData("https://www.dlsite.com/maniax/work/=/product_id/RJ123.html", false)]
    // 網址裡有 steampowered 但網域不對：比對的要是 Host
    [InlineData("https://evil.com/app/620?steampowered", false)]
    public void CanHandle_ChecksHostAndPath(string url, bool expected)
    {
        var source = new SteamSource(new HttpClient());

        var result = source.CanHandle(new Uri(url));

        Assert.Equal(expected, result);
    }

    // 沒有翻譯時 Steam 每個語言都回傳英文名稱，中文和日文應該是空的，而不是三個都一樣
    [Fact]
    public async Task FetchAsync_UntranslatedName_ReturnsEmpty()
    {
        var source = CreateSource(_ => AppJson("Portal"));

        var result = await source.FetchAsync(new Uri("https://store.steampowered.com/app/620/Portal_2/"));

        Assert.NotNull(result);
        Assert.Equal("", result.TitleZh);
        Assert.Equal("", result.TitleJa);
        Assert.Equal("Portal", result.TitleEn);
    }

    [Fact]
    public async Task FetchAsync_NotFound_ReturnsNull()
    {
        var source = CreateSource(_ => """{ "620": { "success": false } }""");

        var result = await source.FetchAsync(new Uri("https://store.steampowered.com/app/620/Portal_2/"));

        Assert.Null(result);
    }

    [Theory]
    [InlineData("18 Apr, 2011", "2011-04-18")]
    [InlineData("Apr 18, 2011", "2011-04-18")]
    [InlineData("15 Oct, 2026", "2026-10-15")]
    [InlineData("Q1 2027", null)]
    public async Task FetchAsync_ParsesReleaseDate(string steamDate, string? expected)
    {
        var source = CreateSource(_ => AppJson("Test", steamDate));

        var result = await source.FetchAsync(new Uri("https://store.steampowered.com/app/620"));

        // InlineData 只能放常數，日期用字串傳進來；用 result! 讓整個抓取失敗時測試也失敗
        var expectedDate = expected is null ? (DateOnly?)null : DateOnly.Parse(expected);
        Assert.Equal(expectedDate, result!.ReleaseDate);
    }

    private static string GenresAndDescription(string description, params string[] genres) =>
        $$"""
        "genres": [{{string.Join(",", genres.Select((g, i) => $$"""{ "id": "{{i}}", "description": "{{g}}" }"""))}}],
        "short_description": "{{description}}",
        """;

    // 防：標籤、簡介取了英文版（原版用繁中）、重複的 genre 沒去掉
    [Fact]
    public async Task FetchAsync_TagsAndDescription_FromChineseVersion()
    {
        var source = CreateSource(language => language switch
        {
            "tchinese" => AppJson("傳送門 2", extra: GenresAndDescription("中文簡介", "動作", "獨立製作", "動作")),
            _ => AppJson("Portal 2", extra: GenresAndDescription("English description", "Action", "Indie"))
        });

        var result = await source.FetchAsync(new Uri("https://store.steampowered.com/app/620"));

        Assert.Equal(["動作", "獨立製作"], result!.Tags);
        Assert.Equal("中文簡介", result.Description);
    }

    // 防：繁中版抓不到時整個抓取失敗，或標籤、簡介變成空的
    [Fact]
    public async Task FetchAsync_NoChineseVersion_FallsBackToEnglish()
    {
        var source = CreateSource(language => language == "tchinese"
            ? """{ "620": { "success": false } }"""
            : AppJson("Portal 2", extra: GenresAndDescription("English description", "Action")));

        var result = await source.FetchAsync(new Uri("https://store.steampowered.com/app/620"));

        Assert.Equal("", result!.TitleZh);
        Assert.Equal(["Action"], result.Tags);
        Assert.Equal("English description", result.Description);
    }

    // 防：表單顯示 &quot; 這種原始的 HTML 實體；前後空白沒去掉
    [Fact]
    public async Task FetchAsync_Description_DecodesHtmlEntities()
    {
        var source = CreateSource(_ => AppJson("Portal 2",
            extra: GenresAndDescription(" Think with &quot;portals&quot; &amp; more ", "Action")));

        var result = await source.FetchAsync(new Uri("https://store.steampowered.com/app/620"));

        Assert.Equal("Think with \"portals\" & more", result!.Description);
    }

    // 防：沒指定區域（幣別依伺服器所在地而變）、整數除法把小數捨去、原價和現價拿反、折扣沒帶回來
    [Fact]
    public async Task FetchAsync_Price_ListPriceAndDiscount_InTaiwanDollars()
    {
        var requests = new List<Uri>();
        var source = CreateSource(_ => AppJson("Portal 2", extra: """
            "price_overview": { "currency": "TWD", "initial": 26900, "final": 13450, "discount_percent": 50 },
            """), requests);

        var result = await source.FetchAsync(new Uri("https://store.steampowered.com/app/620"));

        Assert.Equal(134.50m, result!.Price);
        Assert.Equal(269m, result.ListPrice);
        Assert.Equal(50, result.Discount);
        Assert.Equal("TWD", result.Currency);
        Assert.All(requests, uri => Assert.Contains("cc=tw", uri.Query));
    }

    // 防：沒有 genres、short_description、price_overview（免費遊戲）時丟出 NullReferenceException；幣別變成 null
    [Fact]
    public async Task FetchAsync_NoGenresOrDescription_ReturnsEmpty()
    {
        var source = CreateSource(_ => AppJson("Portal 2"));

        var result = await source.FetchAsync(new Uri("https://store.steampowered.com/app/620"));

        Assert.Empty(result!.Tags);
        Assert.Null(result.Price);
        Assert.Null(result.ListPrice);
        Assert.Equal(0, result.Discount);
        Assert.Equal("", result.Currency);
        Assert.Equal("", result.Description);
    }
}
