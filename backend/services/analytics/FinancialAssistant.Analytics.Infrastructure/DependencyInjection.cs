using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Analytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAnalyticsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool windowsNative = false)
    {
        services.Configure<AnalyticsServiceOptions>(
            configuration.GetSection(AnalyticsServiceOptions.SectionName));
        var options = configuration
            .GetSection(AnalyticsServiceOptions.SectionName)
            .Get<AnalyticsServiceOptions>() ?? new AnalyticsServiceOptions();
        if (windowsNative)
        {
            var settings = configuration.GetSection("Analytics:Storage:Postgres").Get<NativePostgresSettings>()
                ?? throw new InvalidOperationException("Native Analytics persistence configuration is missing.");
            var storage = settings.CreateOptions("analytics");
            using (var validation = settings.CreateProtection()) { }
            services.AddSingleton(_ => settings.CreateProtection());
            services.AddSingleton(provider => new OwnedPostgresStore(storage, provider.GetRequiredService<RecordProtection>()));
            services.AddSingleton<AnalyticsPersistenceContext>();
            services.AddSingleton<IAnalyticsProjectionTransaction>(provider => provider.GetRequiredService<AnalyticsPersistenceContext>());
            services.AddSingleton<PostgresAnalyticsReadModelStore>();
            services.AddSingleton<IAnalyticsReadModelStore>(provider => provider.GetRequiredService<PostgresAnalyticsReadModelStore>());
            services.AddSingleton<PostgresAnalyticsEventPublisher>();
            services.AddSingleton<IAnalyticsEventPublisher>(provider => provider.GetRequiredService<PostgresAnalyticsEventPublisher>());
            services.AddSingleton<PostgresAnalyticsLimitProvider>();
            services.AddSingleton<IAnalyticsDailyLimitProvider>(provider => provider.GetRequiredService<PostgresAnalyticsLimitProvider>());
            services.AddSingleton<IAnalyticsLimitProvider>(provider => provider.GetRequiredService<PostgresAnalyticsLimitProvider>());
            services.AddSingleton<AnalyticsFinancialEventMessageHandler>();
            services.AddSingleton<OwnedStorageHealthCheck>();
            services.AddHealthChecks().AddCheck<OwnedStorageHealthCheck>("analytics-owned-storage", tags: new[] { "ready" });
            return services;
        }
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
