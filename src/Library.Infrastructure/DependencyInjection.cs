using Library.Application.Abstractions;
using Library.Application.Sources;
using Library.Infrastructure.Covers;
using Library.Infrastructure.Data;
using Library.Infrastructure.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Infrastructure;

/// <summary>
/// Infrastructure 層的 DI 登記（資料庫、外部商店、封面），由 Program.cs 呼叫。
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration,
        string coverDirectory)
    {
        // 沒設定連線字串時啟動就失敗，而不是等到第一次查詢
        var connectionString = configuration.GetConnectionString("Default")
                               ?? throw new InvalidOperationException("找不到連線字串 'Default'");

        // 作品同時載入作者和標籤：拆成多個 SQL，避免 JOIN 後列數相乘（笛卡兒積）
        services.AddDbContext<LibraryDbContext>(options =>
            options.UseSqlServer(connectionString,
                sql => sql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

        // 介面要拿到同一個請求的同一個 DbContext；寫成 AddScoped<ILibraryDbContext, LibraryDbContext>() 會建出兩個
        services.AddScoped<ILibraryDbContext>(sp => sp.GetRequiredService<LibraryDbContext>());

        // 商店來源：HttpClient 由 IHttpClientFactory 管理連線池。
        // 同一個物件同時提供作品資料（IStoreSource）與價格（IPriceSource），所以先登記具體類別，兩個介面再指向它
        services.AddHttpClient<SteamSource>(client =>
        {
            client.BaseAddress = new Uri("https://store.steampowered.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddTransient<IStoreSource>(sp => sp.GetRequiredService<SteamSource>());
        services.AddTransient<IPriceSource>(sp => sp.GetRequiredService<SteamSource>());

        services.AddHttpClient<MicrosoftStoreSource>(client =>
        {
            client.BaseAddress = new Uri("https://displaycatalog.mp.microsoft.com/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddTransient<IStoreSource>(sp => sp.GetRequiredService<MicrosoftStoreSource>());
        services.AddTransient<IPriceSource>(sp => sp.GetRequiredService<MicrosoftStoreSource>());

        // 下載任意網站的圖片：不設 BaseAddress；有些網站會擋掉沒有 User-Agent 的請求
        services.AddHttpClient<ICoverDownloader, HttpCoverDownloader>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(12);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; Library/1.0)");
        });

        // 不保存任何請求的狀態，整個程式共用一個
        services.AddSingleton<ICoverStorage>(new LocalCoverStorage(coverDirectory));

        return services;
    }
}
