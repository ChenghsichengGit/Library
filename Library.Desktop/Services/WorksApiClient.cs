using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Library.Desktop.Models;

namespace Library.Desktop.Services;

/// <summary>
/// 呼叫後端 API 的地方：把「方法呼叫」變成 HTTP 請求，再把回傳的 JSON 變回 C# 物件。
/// 所有和後端溝通的程式碼集中在這裡，ViewModel 不需要知道網址和 HTTP 的細節。
/// </summary>
public class WorksApiClient
{
    // API 的位置，和在瀏覽器開 Swagger 用的是同一個
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5265/") };

    /// <summary>GET /api/works?…：依條件取得作品清單。</summary>
    public async Task<List<WorkItem>> GetWorksAsync(WorkListQuery query)
    {
        return await _http.GetFromJsonAsync<List<WorkItem>>("api/works" + ToQueryString(query)) ?? [];
    }

// 把查詢條件組成 ?sort=title&desc=True&q=…；沒給的條件不放進網址，後端就不會篩選
    private static string ToQueryString(WorkListQuery query)
    {
        var parts = new List<string> { $"sort={query.Sort}", $"desc={query.Desc}" };

        // 搜尋字可能有空白、& 或中文，要轉成網址安全的格式，不然 & 會被當成下一個參數
        if (!string.IsNullOrWhiteSpace(query.Q))
            parts.Add($"q={Uri.EscapeDataString(query.Q.Trim())}");
        if (query.Favorite is { } favorite)
            parts.Add($"favorite={favorite}");
        if (query.MinScore is { } min)
            parts.Add($"minScore={min}");
        if (query.MaxScore is { } max)
            parts.Add($"maxScore={max}");

        return "?" + string.Join("&", parts);
    }

    /// <summary>POST /api/works：新增作品，回傳建立好的作品（含新的 Id）。驗證失敗丟 ApiValidationException。</summary>
    public async Task<WorkItem> CreateAsync(SaveWorkRequest request)
    {
        // 把 request 轉成 JSON，用 POST 送出
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

// 400 是使用者可以自己修正的錯誤，轉成驗證例外；其他失敗交給 EnsureSuccessStatusCode 丟出 HttpRequestException
    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // 400 的回應長這樣：{ "errors": { "TitleZh": ["至少輸入一個名稱"], "TitleJa": [...] } }
            // 同一句訊息可能掛在好幾個欄位上，攤平後去掉重複
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
            var messages = problem?.Errors.Values.SelectMany(m => m).Distinct().ToList()
                           ?? ["輸入的資料有誤"];
            throw new ApiValidationException(messages);
        }

        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// GET /api/lookup?url=…：用商店網址查詢作品資料，不會存檔。
    /// 不支援的網址、找不到作品、連不上商店時，丟出帶有 API 訊息的 ApiValidationException。
    /// </summary>
    public async Task<StoreWorkInfo> LookupAsync(string url)
    {
        // 網址裡又放了一個網址：Steam 網址裡的 & ? / 不轉換的話，& 會被當成外層網址的下一個參數
        var response = await _http.GetAsync($"api/lookup?url={Uri.EscapeDataString(url)}");

        // LookupController 的 400／404／502 內容都是一段純文字，不是 EnsureSuccessAsync 處理的 { "errors": … } 格式
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new ApiValidationException([message]);
        }

        return (await response.Content.ReadFromJsonAsync<StoreWorkInfo>())!;
    }

// ASP.NET Core 驗證失敗時回傳的 JSON 裡，只需要 errors 這個欄位
    private record ValidationProblem(Dictionary<string, string[]> Errors);
}