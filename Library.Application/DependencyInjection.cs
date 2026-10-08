using Library.Application.Services;
using Library.Application.Sources;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Application;

/// <summary>
/// Application 層的 DI 登記，相當於這一層的 Init()。由 Program.cs 呼叫。
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 把 Application 需要的東西登記到 DI 容器；新增 Service 時只改這個檔案，Program.cs 不用動。
    /// </summary>
    /// <remarks>
    /// 參數前面的 this 讓它變成擴充方法，才能寫成 builder.Services.AddApplication()。
    /// 這裡只是登記，執行時什麼都還沒建立。
    /// </remarks>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // 有人要 WorkService 時 new 一個給他；Scoped = 每個 HTTP 請求一個，請求結束就丟掉
        services.AddScoped<WorkService>();

        // 有人要 TimeProvider 時給系統時鐘；Singleton = 整個程式共用一個（時鐘沒有狀態，共用沒問題）
        services.AddSingleton(TimeProvider.System);

        // 建構子要的 IEnumerable<IStoreSource>，容器會把所有登記成 IStoreSource 的實作（在 AddInfrastructure）一次給它
        services.AddScoped<StoreLookupService>();

        // 回傳自己，呼叫的地方才能接著寫 .AddXxx()
        return services;
    }
}
