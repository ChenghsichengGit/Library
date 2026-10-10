using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Library.Application.Sources;

namespace Library.Infrastructure.Sources;

/// <summary>
/// Steam 商店的來源：從網址取出 App ID，呼叫 Steam 的 appdetails API 抓回名稱、開發商、發行商、上架日。
/// </summary>
/// <remarks>
/// HttpClient 從建構子傳入，測試時可以換成裝了假 HttpMessageHandler 的版本。連線失敗的例外交給呼叫端處理。
/// </remarks>
public partial class SteamSource : IStoreSource
{
    private readonly HttpClient _http;

    public SteamSource(HttpClient http) => _http = http;

    // /app/620/Portal_2/ → 620
    [GeneratedRegex(@"^/app/(\d+)")]
    private static partial Regex AppIdPattern();

    /// <summary>網域是 Steam 商店、路徑是 /app/數字 才處理。</summary>
    // 比對 Host 而不是整個網址的文字，evil.com/?steampowered 才不會通過
    public bool CanHandle(Uri url) =>
        url.Host == "store.steampowered.com" && AppIdPattern().IsMatch(url.AbsolutePath);

    /// <summary>
    /// 抓取作品資料，找不到這個遊戲回傳 null。名稱依語言各呼叫一次；開發商、發行商、日期取英文版（日期格式較固定）。
    /// </summary>
    public async Task<StoreWorkInfo?> FetchAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var appId = AppIdPattern().Match(url.AbsolutePath).Groups[1].Value;

        // 三次呼叫互不相關，同時送出，總時間約等於最慢的那一次
        var zhTask = GetAppAsync(appId, "tchinese", cancellationToken);
        var jaTask = GetAppAsync(appId, "japanese", cancellationToken);
        var enTask = GetAppAsync(appId, "english", cancellationToken);
        await Task.WhenAll(zhTask, jaTask, enTask);

        var en = enTask.Result;
        if (en is null)
            return null;

        return new StoreWorkInfo(
            TitleZh: Translated(zhTask.Result, en.Name),
            TitleJa: Translated(jaTask.Result, en.Name),
            TitleEn: en.Name,
            Authors: en.Developers ?? [],
            Circles: en.Publishers ?? [],
            ReleaseDate: ParseDate(en.ReleaseDate));
    }

    // 沒有這個語言的名稱時 Steam 會回傳英文名稱，和英文一樣就當作沒有翻譯
    private static string Translated(SteamApp? app, string english) =>
        app?.Name is { } name && name != english ? name : "";

    private async Task<SteamApp?> GetAppAsync(string appId, string language, CancellationToken cancellationToken)
    {
        // 最外層的 key 是 App ID（{ "620": {...} }），每個遊戲不同，所以用 Dictionary 接
        var response = await _http.GetFromJsonAsync<Dictionary<string, SteamResponse>>(
            $"api/appdetails?appids={appId}&l={language}", JsonOptions, cancellationToken);

        // 格式錯誤的 App ID，Steam 會回傳 null
        if (response is null || !response.TryGetValue(appId, out var app))
            return null;

        return app.Success ? app.Data : null;
    }

    // 只採用完整的年月日；Q1 2027、Coming soon 是 null，不讓整個抓取失敗。即將推出的遊戲也可能有確定日期，所以不看 coming_soon
    private static DateOnly? ParseDate(SteamReleaseDate? releaseDate)
    {
        if (releaseDate is null)
            return null;

        // 不依電腦語系，Apr 才認得
        return DateOnly.TryParseExact(releaseDate.Date, ["d MMM, yyyy", "MMM d, yyyy"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    // Steam 的欄位是 release_date 這種底線命名
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // Steam 回應的形狀，只宣告需要的欄位；private 讓 Steam 的格式不會流出這個類別
    private record SteamResponse(bool Success, SteamApp? Data);

    private record SteamApp(string Name, string[]? Developers, string[]? Publishers, SteamReleaseDate? ReleaseDate);

    private record SteamReleaseDate(string Date);
}
