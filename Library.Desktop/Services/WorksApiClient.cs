using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Library.Desktop.Models;

namespace Library.Desktop.Services;

// 所有和後端 API 溝通的程式碼集中在這裡，ViewModel 不需要知道網址和 HTTP 的細節
public class WorksApiClient
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("http://localhost:5265/") };

    public async Task<List<WorkItem>> GetWorksAsync()
    {
        return await _http.GetFromJsonAsync<List<WorkItem>>("api/works") ?? [];
    }
    
    public async Task<WorkItem> CreateAsync(SaveWorkRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/works", request);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<WorkItem>())!;
    }

    public async Task UpdateAsync(int id, SaveWorkRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/works/{id}", request);
        await EnsureSuccessAsync(response);
    }

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
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>();
            var messages = problem?.Errors.Values.SelectMany(m => m).Distinct().ToList()
                           ?? ["輸入的資料有誤"];
            throw new ApiValidationException(messages);
        }

        response.EnsureSuccessStatusCode();
    }

// ASP.NET Core 驗證失敗時回傳的 JSON 裡，只需要 errors 這個欄位
    private record ValidationProblem(Dictionary<string, string[]> Errors);
}