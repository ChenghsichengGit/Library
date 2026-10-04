using Library.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Application;

public static class DependencyInjection
{
    // Application 需要登記的東西都在這裡，新增 Service 時只改這個檔案
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<WorkService>();
        return services;
    }
}