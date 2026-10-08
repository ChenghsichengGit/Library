using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Library.Application.Sources;

namespace Library.Infrastructure.Sources;

/// <summary>
/// Steam 商店的來源：從商店網址取出 App ID，呼叫 Steam 的公開 API（appdetails）抓回名稱、開發商、發行商、上架日。
/// </summary>
/// <remarks>
/// HttpClient 由 IHttpClientFactory 提供（見 DependencyInjection），BaseAddress 已設成 store.steampowered.com；
/// 測試時可以傳入裝了假 HttpMessageHandler 的 HttpClient，不會真的連到 Steam。
/// 連線失敗、狀態碼不是 2xx 時，GetFromJsonAsync 會丟出例外，由呼叫的一方處理。
/// </remarks>
public partial class SteamSource : IStoreSource
{
    private readonly HttpClient _http;

    public SteamSource(HttpClient http) => _http = http;

    // 從網址的路徑取出 App ID：/app/620/Portal_2/ → 620
    // GeneratedRegex：編譯時就產生比對程式碼，規則寫錯會在編譯時報錯
    [GeneratedRegex(@"^/app/(\d+)")]
    private static partial Regex AppIdPattern();

    /// <summary>網域是 Steam 商店、路徑是 /app/數字 才處理。只看網址，不連網路。</summary>
    // 比對 Host 而不是整個網址的文字：evil.com/?steampowered 這種網址才不會通過
    public bool CanHandle(Uri url) =>
        url.Host == "store.steampowered.com" && AppIdPattern().IsMatch(url.AbsolutePath);

    /// <summary>
    /// 抓取作品資料。找不到這個遊戲（success 為 false）回傳 null。
    /// </summary>
    /// <remarks>
    /// 名稱要用 l=tchinese／japanese／english 各呼叫一次；開發商、發行商、日期取英文那一次（日期格式較固定）。
    /// </remarks>
    public async Task<StoreWorkInfo?> FetchAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var appId = AppIdPattern().Match(url.AbsolutePath).Groups[1].Value;

        // 三次呼叫互不相關，先全部送出再一起等，總時間約等於最慢的那一次
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

    // 呼叫一次 Steam API，取出 data；找不到（success 為 false）回傳 null
    private async Task<SteamApp?> GetAppAsync(string appId, string language, CancellationToken cancellationToken)
    {
        // 回應最外層的 key 是 App ID（{ "620": {...} }），每個遊戲都不同，所以用 Dictionary 接
        var response = await _http.GetFromJsonAsync<Dictionary<string, SteamResponse>>(
            $"api/appdetails?appids={appId}&l={language}", JsonOptions, cancellationToken);

        // 格式錯誤的 App ID，Steam 會回傳 null
        if (response is null || !response.TryGetValue(appId, out var app))
            return null;

        return app.Success ? app.Data : null;
    }

    // 只有完整的年月日才採用；Q1 2027、Coming soon 這類解析不出來就是 null，不讓整個抓取失敗
    // 即將推出（coming_soon）的遊戲也可能已有確定日期，所以不看 coming_soon
    private static DateOnly? ParseDate(SteamReleaseDate? releaseDate)
    {
        if (releaseDate is null)
            return null;

        // InvariantCulture：用固定的英文規則解析，Apr 才認得；不加的話會依電腦的語系解析
        return DateOnly.TryParseExact(releaseDate.Date, ["d MMM, yyyy", "MMM d, yyyy"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    // Steam 的 JSON 欄位是 release_date 這種底線命名，C# 是 ReleaseDate，用命名規則自動對應
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // Steam 回應的形狀，只宣告需要的欄位，其他會被忽略；設成 private，Steam 的格式不會流出這個類別
    // 名稱對不上時不會報錯而是得到預設值，靠 SteamSourceTests 確認每個欄位都有拿到
    private record SteamResponse(bool Success, SteamApp? Data);

    private record SteamApp(string Name, string[]? Developers, string[]? Publishers, SteamReleaseDate? ReleaseDate);

    private record SteamReleaseDate(string Date);
}
