using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Library.Desktop.Models;

namespace Library.Desktop.Services;

/// <summary>
/// 呼叫後端 API：方法呼叫變成 HTTP 請求，JSON 變回 C# 物件。ViewModel 不需要知道網址和 HTTP 的細節。
/// </summary>
public class WorksApiClient
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5265/") };

    /// <summary>GET /api/works?…：依條件取得作品清單。</summary>
    public async Task<List<WorkItem>> GetWorksAsync(WorkListQuery query)
    {
        return await _http.GetFromJsonAsync<List<WorkItem>>("api/works" + ToQueryString(query)) ?? [];
    }

    // 沒給的條件不放進網址，後端就不篩選
    private static string ToQueryString(WorkListQuery query)
    {
        var parts = new List<string> { $"sort={query.Sort}", $"desc={query.Desc}" };

        // 搜尋字裡的 & 不轉換的話會被當成下一個參數
        if (!string.IsNullOrWhiteSpace(query.Q))
            parts.Add($"q={Uri.EscapeDataString(query.Q.Trim())}");
        if (query.Favorite is { } favorite)
            parts.Add($"favorite={favorite}");
        if (query.MinScore is { } min)
            parts.Add($"minScore={min}");
        if (query.MaxScore is { } max)
            parts.Add($"maxScore={max}");
        if (query.WorkTypeId is { } workTypeId)
            parts.Add($"workTypeId={workTypeId}");
        if (query.CreatorId is { } creatorId)
            parts.Add($"creatorId={creatorId}");
        if (query.SeriesId is { } seriesId)
            parts.Add($"seriesId={seriesId}");
        if (query.DlcUnbought)
            parts.Add("dlcUnbought=True");

        // 每個標籤各一個 tags=；預設是全部符合，只有「符合任一」才送 tagMode
        foreach (var tag in query.Tags)
            parts.Add($"tags={Uri.EscapeDataString(tag)}");
        if (query.TagAny)
            parts.Add("tagMode=Any");

        return "?" + string.Join("&", parts);
    }

    /// <summary>POST /api/works：新增作品，回傳建立好的作品（含新的 Id）。驗證失敗丟 ApiValidationException。</summary>
    public async Task<WorkItem> CreateAsync(SaveWorkRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/works", request);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<WorkItem>())!;
    }

    /// <summary>PUT /api/works/{id}：修改作品。驗證失敗丟 ApiValidationException。</summary>
    public async Task UpdateAsync(int id, SaveWorkRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/works/{id}", request);
        await EnsureSuccessAsync(response);
    }

    /// <summary>DELETE /api/works/{id}：刪除作品（後端是軟刪除）。</summary>
    public async Task DeleteAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/works/{id}");
        await EnsureSuccessAsync(response);
    }

    // 400 是使用者可以修正的錯誤，轉成驗證例外；其他失敗由 EnsureSuccessStatusCode 丟出 HttpRequestException
    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // { "errors": { "TitleZh": ["至少輸入一個名稱"], ... } }；同一句訊息可能掛在好幾個欄位上，所以去重複
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
            var messages = problem?.Errors.Values.SelectMany(m => m).Distinct().ToList()
                           ?? ["輸入的資料有誤"];
            throw new ApiValidationException(messages);
        }

        // 409（名稱重複等）的內容是純文字，不是 { "errors": … }
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new ApiValidationException([message]);
        }

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// GET /api/lookup?url=…：用商店網址查詢作品資料，不會存檔。
    /// 不支援的網址、找不到作品、連不上商店時，丟出帶有 API 訊息的 ApiValidationException。
    /// </summary>
    public async Task<StoreWorkInfo> LookupAsync(string url)
    {
        // 網址裡又放了一個網址，裡面的 & 要轉換
        var response = await _http.GetAsync($"api/lookup?url={Uri.EscapeDataString(url)}");

        // LookupController 的錯誤是純文字，不是 { "errors": … } 格式
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new ApiValidationException([message]);
        }

        return (await response.Content.ReadFromJsonAsync<StoreWorkInfo>())!;
    }

    /// <summary>GET /api/work-types：類型清單，依顯示順序排好。</summary>
    public async Task<List<WorkTypeItem>> GetWorkTypesAsync()
    {
        return await _http.GetFromJsonAsync<List<WorkTypeItem>>("api/work-types") ?? [];
    }

    /// <summary>POST /api/work-types：新增類型。名稱空白或重複丟 ApiValidationException。</summary>
    public async Task<WorkTypeItem> CreateWorkTypeAsync(string name, int? sortOrder)
    {
        var response = await _http.PostAsJsonAsync("api/work-types",
            new SaveWorkTypeRequest(name, sortOrder));
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<WorkTypeItem>())!;
    }

    /// <summary>PUT /api/work-types/{id}：修改名稱與順序。</summary>
    public async Task UpdateWorkTypeAsync(int id, string name, int? sortOrder)
    {
        var response = await _http.PutAsJsonAsync($"api/work-types/{id}",
            new SaveWorkTypeRequest(name, sortOrder));
        await EnsureSuccessAsync(response);
    }

    /// <summary>DELETE /api/work-types/{id}：刪除類型，使用它的作品改成「其他」。刪除「其他」丟 ApiValidationException。</summary>
    public async Task DeleteWorkTypeAsync(int id)
    {
        var response = await _http.DeleteAsync($"api/work-types/{id}");
        await EnsureSuccessAsync(response);
    }

    /// <summary>GET /api/tags：有作品在用的標籤與作品數。</summary>
    public async Task<List<TagItem>> GetTagsAsync()
    {
        return await _http.GetFromJsonAsync<List<TagItem>>("api/tags") ?? [];
    }

    /// <summary>GET /api/series：有作品的系列，給系列輸入框當選項。</summary>
    public async Task<List<SeriesItem>> GetSeriesAsync(int? creatorId = null)
    {
        var url = creatorId is { } id ? $"api/series?creatorId={id}" : "api/series";
        return await _http.GetFromJsonAsync<List<SeriesItem>>(url) ?? [];
    }

    /// <summary>POST /api/covers：上傳圖片（multipart，欄位名稱 file）。不是圖片、太大時丟 ApiValidationException。</summary>
    public async Task<CoverUploadResult> UploadCoverAsync(byte[] content, string fileName)
    {
        // multipart 的一個欄位：名稱要和 CoversController.Upload 的參數名稱 file 一樣
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(content), "file", fileName);

        var response = await _http.PostAsync("api/covers", form);
        return await ReadCoverResultAsync(response);
    }

    /// <summary>POST /api/covers/download：請 API 從網址下載圖片。下載失敗時丟 ApiValidationException。</summary>
    public async Task<CoverUploadResult> DownloadCoverAsync(string url)
    {
        // 對應後端的 DownloadRequest(string Url)，JSON 是 { "url": "…" }
        var response = await _http.PostAsJsonAsync("api/covers/download", new { Url = url });
        return await ReadCoverResultAsync(response);
    }

    // CoversController 的錯誤是純文字（400、502）；413 由伺服器直接擋下，沒有內容
    private static async Task<CoverUploadResult> ReadCoverResultAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
            throw new ApiValidationException(["封面不能超過 10MB"]);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new ApiValidationException([message]);
        }

        return (await response.Content.ReadFromJsonAsync<CoverUploadResult>())!;
    }

    /// <summary>GET /api/creators?role=author：作者（或 circle 社團）與作品數，給自動完成用。</summary>
    public async Task<List<CreatorItem>> GetCreatorsAsync(string role)
    {
        return await _http.GetFromJsonAsync<List<CreatorItem>>($"api/creators?role={role}") ?? [];
    }

    /// <summary>PUT /api/creators/{id}：修改作者／社團。撞名、格式錯誤丟 ApiValidationException。</summary>
    public async Task UpdateCreatorAsync(int id, string name, string url, string note, string? coverKey)
    {
        // 空的網址送 null：後端的 [Url] 不接受空字串
        var response = await _http.PutAsJsonAsync($"api/creators/{id}",
            new { Name = name, Url = url == "" ? null : url, Note = note, CoverKey = coverKey });
        await EnsureSuccessAsync(response);
    }

    /// <summary>PUT /api/series/{id}：修改系列。</summary>
    public async Task UpdateSeriesAsync(int id, string name, string note, string? coverKey)
    {
        var response =
            await _http.PutAsJsonAsync($"api/series/{id}", new { Name = name, Note = note, CoverKey = coverKey });
        await EnsureSuccessAsync(response);
    }

    /// <summary>GET /api/creators/{id}/cover-choices：這個人參與的作品的封面。</summary>
    public async Task<List<CoverChoice>> GetCreatorCoverChoicesAsync(int id)
    {
        return await _http.GetFromJsonAsync<List<CoverChoice>>($"api/creators/{id}/cover-choices") ?? [];
    }

    /// <summary>GET /api/series/{id}/cover-choices：系列裡作品的封面。</summary>
    public async Task<List<CoverChoice>> GetSeriesCoverChoicesAsync(int id)
    {
        return await _http.GetFromJsonAsync<List<CoverChoice>>($"api/series/{id}/cover-choices") ?? [];
    }

    /// <summary>API 回傳的相對網址（/covers/…）轉成完整網址，給 Image 顯示。</summary>
    public string ToAbsoluteUrl(string relativeUrl) => new Uri(_http.BaseAddress!, relativeUrl).ToString();

    // ASP.NET Core 驗證失敗的回應，只取 errors
    private record ValidationProblem(Dictionary<string, string[]> Errors);
}
