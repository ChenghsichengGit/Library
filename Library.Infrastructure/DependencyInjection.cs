using Library.Application.Abstractions;
using Library.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Infrastructure;

public static class DependencyInjection
{
    // 資料庫相關的登記都在這裡；換資料庫時只改這個檔案，Api 不用動
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // 連線字串沒設定時，啟動當下就失敗，而不是等到第一次查詢才出現看不懂的錯誤
        var connectionString = configuration.GetConnectionString("Default")
                               ?? throw new InvalidOperationException("找不到連線字串 'Default'");

        services.AddDbContext<LibraryDbContext>(options => options.UseSqlServer(connectionString));

        // 同一個請求內，要 ILibraryDbContext 時給的是同一個 LibraryDbContext
        services.AddScoped<ILibraryDbContext>(sp => sp.GetRequiredService<LibraryDbContext>());

        return services;
    }
}