using System.Text.Json;
using System.Text.Json.Serialization;
using Library.Application;
using Library.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── 登記服務 ──
// enum 在 JSON 裡用名稱（"ja"）而不是數字，Swagger 也會列出可用的值
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// ── 請求管線 ──
// Swagger 只在開發環境開放
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "v1"));
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
