using FinancialAssistant.Analytics.Application;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Analytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAnalyticsInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryAnalyticsReadModelStore>();
        services.AddSingleton<IAnalyticsReadModelStore>(provider =>
            provider.GetRequiredService<InMemoryAnalyticsReadModelStore>());
        return services;
    }
}
