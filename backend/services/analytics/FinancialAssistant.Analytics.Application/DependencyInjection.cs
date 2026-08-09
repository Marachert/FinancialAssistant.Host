using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Analytics.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAnalyticsApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AnalyticsRebuildPlanner>();
        services.AddSingleton<AnalyticsProjector>();
        return services;
    }
}
