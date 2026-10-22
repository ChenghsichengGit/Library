using System.Text.Json;
using System.Text.Json.Serialization;
using Library.Application;
using Library.Application.Services;
using Library.Infrastructure;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// ── 登記服務 ──
// enum 在 API 的 JSON 裡用名稱（"ja"）而不是數字
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddOpenApi();

// 封面存在 API 專案底下的 covers 資料夾
var coverDirectory = Path.Combine(builder.Environment.ContentRootPath, "covers");
Directory.CreateDirectory(coverDirectory);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, coverDirectory);

var app = builder.Build();

// ── 啟動時的工作 ──
// 刪除超過 30 天的作品永久清除（同原版）。這時還沒有請求，要自己建立 scope 才拿得到 Scoped 的服務
using (var scope = app.Services.CreateScope())
{
    var trash = scope.ServiceProvider.GetRequiredService<TrashService>();
    var purged = await trash.PurgeExpiredAsync();
    if (purged > 0)
        app.Logger.LogInformation("已永久刪除超過 {Days} 天的作品 {Count} 部", TrashService.KeepDays, purged);
}

// ── 請求管線 ──
// Swagger 只在開發環境開放
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "v1"));
}

app.UseHttpsRedirection();

// 封面圖片：/covers/xxx 對應到 coverDirectory 裡的 xxx，和 LocalCoverStorage.GetUrl 組出的網址一致
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(coverDirectory),
    RequestPath = "/covers"
});

app.UseAuthorization();

app.MapControllers();

app.Run();
