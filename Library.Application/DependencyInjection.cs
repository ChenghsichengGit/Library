using Library.Application.Services;
using Library.Application.Sources;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Application;

/// <summary>
/// Application 層的 DI 登記，由 Program.cs 呼叫。
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<WorkService>();
        services.AddScoped<WorkTypeService>();

        // 時鐘沒有狀態，整個程式共用一個
        services.AddSingleton(TimeProvider.System);

        // 建構子的 IEnumerable<IStoreSource> 會拿到 AddInfrastructure 登記的所有來源
        services.AddScoped<StoreLookupService>();

        return services;
    }
}
