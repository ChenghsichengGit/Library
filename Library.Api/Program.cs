using System.Text.Json;
using System.Text.Json.Serialization;
using Library.Application;
using Library.Infrastructure;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// ── 登記服務 ──
// enum 在 JSON 裡用名稱（"ja"）而不是數字，Swagger 也會列出可用的值
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
