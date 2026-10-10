using Library.Application.Abstractions;
using Library.Application.Sources;
using Library.Infrastructure.Data;
using Library.Infrastructure.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Infrastructure;

/// <summary>
/// Infrastructure 層的 DI 登記（資料庫、外部商店），由 Program.cs 呼叫。
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 沒設定連線字串時啟動就失敗，而不是等到第一次查詢
        var connectionString = configuration.GetConnectionString("Default")
                               ?? throw new InvalidOperationException("找不到連線字串 'Default'");

        services.AddDbContext<LibraryDbContext>(options => options.UseSqlServer(connectionString));

        // 介面要拿到同一個請求的同一個 DbContext；寫成 AddScoped<ILibraryDbContext, LibraryDbContext>() 會建出兩個
        services.AddScoped<ILibraryDbContext>(sp => sp.GetRequiredService<LibraryDbContext>());

        // HttpClient 由 IHttpClientFactory 管理連線池
        services.AddHttpClient<IStoreSource, SteamSource>(client =>
        {
            client.BaseAddress = new Uri("https://store.steampowered.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }
}
