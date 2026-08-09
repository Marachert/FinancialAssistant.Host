using FinancialAssistant.Analytics.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Analytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAnalyticsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<AnalyticsServiceOptions>(
            configuration.GetSection(AnalyticsServiceOptions.SectionName));
        var options = configuration
            .GetSection(AnalyticsServiceOptions.SectionName)
            .Get<AnalyticsServiceOptions>() ?? new AnalyticsServiceOptions();
        services.AddSingleton<InMemoryAnalyticsReadModelStore>();
        services.AddSingleton<IAnalyticsReadModelStore>(provider =>
            provider.GetRequiredService<InMemoryAnalyticsReadModelStore>());
        services.AddSingleton<InMemoryAnalyticsDailyLimitProvider>();
        services.AddSingleton<IAnalyticsDailyLimitProvider>(provider =>
            provider.GetRequiredService<InMemoryAnalyticsDailyLimitProvider>());
        services.AddSingleton<IAnalyticsLimitProvider>(provider =>
            provider.GetRequiredService<InMemoryAnalyticsDailyLimitProvider>());
        if (string.Equals(options.Events.Mode, "RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAnalyticsEventPublisher, RabbitMqAnalyticsEventPublisher>();
        }
        else
        {
            services.AddSingleton<InMemoryAnalyticsEventPublisher>();
            services.AddSingleton<IAnalyticsEventPublisher>(provider =>
                provider.GetRequiredService<InMemoryAnalyticsEventPublisher>());
        }
        services.AddSingleton<AnalyticsFinancialEventMessageHandler>();
        services.AddHostedService<AnalyticsFinancialEventConsumer>();
        return services;
    }
}
