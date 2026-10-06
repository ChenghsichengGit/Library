// 程式的進入點：按下 ▶ 或 dotnet run 時，從這個檔案的第一行開始執行。
// 分成兩個階段：① 登記服務（啟動時一次）② 組裝請求管線（之後每個請求都會經過）。

using Library.Application;
using Library.Infrastructure;

// 讀取設定：appsettings.json → appsettings.Development.json → User Secrets（後面的會蓋掉前面的）
var builder = WebApplication.CreateBuilder(args);

// ── 階段一：登記服務 ──
// 這裡只是「登記」到 DI 容器（告訴它需要時怎麼建立），還沒有真的建立任何東西，也還沒連資料庫

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddApplication();                            // WorkService、TimeProvider（見 Application/DependencyInjection.cs）
builder.Services.AddInfrastructure(builder.Configuration);    // DbContext、連線字串（見 Infrastructure/DependencyInjection.cs）

// 登記結束，DI 容器封存，之後不能再登記
var app = builder.Build();

// ── 階段二：組裝請求管線 ──
// 之後每個 HTTP 請求都會依照下面的順序一關一關通過

// Swagger 只在開發環境開放，正式環境不會出現
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // 讀取上面產生的 /openapi/v1.json，顯示成可以直接試打 API 的網頁
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "v1"));
}

app.UseHttpsRedirection();

app.UseAuthorization();

// 掃描所有 Controller 的 [Route]、[HttpGet]…，建立「網址 → 方法」的路由表
app.MapControllers();

// 開始監聽（http://localhost:5265），程式停在這裡等請求進來
app.Run();
