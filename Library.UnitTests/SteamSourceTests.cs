using System.Net;
using System.Text;
using Library.Infrastructure.Sources;

namespace Library.UnitTests;

/// <summary>
/// SteamSource 的單元測試：用假的 HttpMessageHandler 取代網路，不會真的連到 Steam。
/// </summary>
/// <remarks>
/// HttpClient 只是外殼，真正送出請求的是裡面的 handler；把它換成假的，就能決定「Steam」回傳什麼。
/// JSON 的欄位名稱對不上時不會報錯，只會得到預設值，所以每個欄位都要斷言。
/// </remarks>
public class SteamSourceTests
{
    // 假的 HttpMessageHandler：不連網路，把請求交給 respond 決定回傳什麼
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

    // 依照網址的 l= 參數回傳不同語言的 JSON，模擬 Steam 的行為
    private static SteamSource CreateSource(Func<string, string> jsonForLanguage) =>
        new(new HttpClient(new FakeHandler(request =>
        {
            var language = request.RequestUri!.Query.Split("l=")[1];
            return jsonForLanguage(language);
        }))
        {
            // 正式執行時在 DI 設定，測試不經過 DI，要自己設
            BaseAddress = new Uri("https://store.steampowered.com/")
        });

    // developers 和 publishers 刻意不同，作者和社團拿反時才抓得到
    private static string AppJson(string name, string date = "18 Apr, 2011") => $$"""
        {
          "620": {
            "success": true,
            "data": {
              "name": "{{name}}",
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
    }

    [Theory]
    [InlineData("https://store.steampowered.com/app/620/Portal_2/", true)]
    [InlineData("https://store.steampowered.com/app/620", true)]
    [InlineData("https://store.steampowered.com/search/?term=portal", false)]
    [InlineData("https://www.dlsite.com/maniax/work/=/product_id/RJ123.html", false)]
    // 網址裡有 steampowered 但網域不對：確認比對的是 Host，不是整個網址的文字
    [InlineData("https://evil.com/app/620?steampowered", false)]
    public void CanHandle_ChecksHostAndPath(string url, bool expected)
    {
        // CanHandle 不連網路，給一般的 HttpClient 就好
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

        // InlineData 只能放常數，預期的日期用字串傳進來再轉成 DateOnly?，和結果用同一個型別比較
        // result! 而不是 result?.：整個抓取失敗（null）時要讓測試失敗，不能和預期的 null 混在一起
        var expectedDate = expected is null ? (DateOnly?)null : DateOnly.Parse(expected);
        Assert.Equal(expectedDate, result!.ReleaseDate);
    }
}