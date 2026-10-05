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
}