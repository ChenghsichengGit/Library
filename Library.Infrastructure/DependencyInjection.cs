using Library.Application.Abstractions;
using Library.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Infrastructure;

/// <summary>
/// Infrastructure 層的 DI 登記，相當於這一層的 Init()。由 Program.cs 呼叫。
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 登記資料庫相關的東西。換資料庫時只改這個檔案，Api 不用動。
    /// </summary>
    /// <param name="configuration">設定檔，用來讀連線字串（開發時來自 User Secrets）。</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 讀連線字串：ConnectionStrings:Default。這行是「現在」就執行的（不是登記）
        // 連線字串沒設定時，啟動當下就失敗，而不是等到第一次查詢才出現看不懂的錯誤
        var connectionString = configuration.GetConnectionString("Default")
                               ?? throw new InvalidOperationException("找不到連線字串 'Default'");

        // 有人要 LibraryDbContext 時建立一個，並設定成「用 SQL Server、連到這個連線字串」；預設是 Scoped
        services.AddDbContext<LibraryDbContext>(options => options.UseSqlServer(connectionString));

        // 有人要 ILibraryDbContext 時，去容器拿「這個請求的那個 LibraryDbContext」（sp = 容器本身）
        // 不寫成 AddScoped<ILibraryDbContext, LibraryDbContext>()：那樣同一個請求會有兩個不同的 DbContext，
        // 在其中一個改的資料，另一個 SaveChanges 時不會存進去
        services.AddScoped<ILibraryDbContext>(sp => sp.GetRequiredService<LibraryDbContext>());

        return services;
    }
}
