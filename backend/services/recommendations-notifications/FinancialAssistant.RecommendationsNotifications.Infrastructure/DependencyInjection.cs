using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRecommendationNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool windowsNative = false)
    {
        services.Configure<RecommendationNotificationServiceOptions>(
            configuration.GetSection(RecommendationNotificationServiceOptions.SectionName));
        var options = configuration
            .GetSection(RecommendationNotificationServiceOptions.SectionName)
            .Get<RecommendationNotificationServiceOptions>() ??
            new RecommendationNotificationServiceOptions();

        if (windowsNative)
        {
            var settings = configuration.GetSection("RecommendationsNotifications:Storage:Postgres").Get<NativePostgresSettings>()
                ?? throw new InvalidOperationException("Native insight persistence configuration is missing.");
            var storage = settings.CreateOptions("recommendations-notifications");
            using (var validation = settings.CreateProtection()) { }
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(_ => settings.CreateProtection());
            services.AddSingleton(provider => new OwnedPostgresStore(storage, provider.GetRequiredService<RecordProtection>()));
            services.AddSingleton<InsightPersistenceContext>();
            services.AddSingleton<IInsightProcessingTransaction>(provider => provider.GetRequiredService<InsightPersistenceContext>());
            services.AddSingleton<PostgresRecommendationNotificationStore>();
            services.AddSingleton<IRecommendationNotificationStore>(provider => provider.GetRequiredService<PostgresRecommendationNotificationStore>());
            services.AddSingleton<PostgresRecommendationProfileSettingsProvider>();
            services.AddSingleton<IRecommendationProfileSettingsProvider>(provider => provider.GetRequiredService<PostgresRecommendationProfileSettingsProvider>());
            services.AddSingleton<PostgresNotificationPreferenceProvider>();
            services.AddSingleton<INotificationPreferenceProvider>(provider => provider.GetRequiredService<PostgresNotificationPreferenceProvider>());
            services.AddSingleton<PostgresInsightEventPublisher>();
            services.AddSingleton<IRecommendationEventPublisher>(provider => provider.GetRequiredService<PostgresInsightEventPublisher>());
            services.AddSingleton<INotificationEventPublisher>(provider => provider.GetRequiredService<PostgresInsightEventPublisher>());
            services.AddSingleton<INotificationDeliveryAdapter, MobilePushNotificationDeliveryAdapter>();
            services.AddSingleton<INotificationDeliveryAdapter, WebNotificationDeliveryAdapter>();
            services.AddSingleton<INotificationRetryPolicy, ConfiguredNotificationRetryPolicy>();
            services.AddSingleton<RecommendationNotificationMessageHandler>();
            services.AddSingleton<OwnedStorageHealthCheck>();
            services.AddHealthChecks().AddCheck<OwnedStorageHealthCheck>("insight-owned-storage", tags: new[] { "ready" });
            return services;
        }

        services.AddSingleton<InMemoryRecommendationNotificationStore>();
        services.AddSingleton<IRecommendationNotificationStore>(provider =>
            provider.GetRequiredService<InMemoryRecommendationNotificationStore>());
        services.AddSingleton<InMemoryRecommendationProfileSettingsProvider>();
        services.AddSingleton<IRecommendationProfileSettingsProvider>(provider =>
            provider.GetRequiredService<InMemoryRecommendationProfileSettingsProvider>());
        services.AddSingleton<InMemoryNotificationPreferenceProvider>();
        services.AddSingleton<INotificationPreferenceProvider>(provider =>
            provider.GetRequiredService<InMemoryNotificationPreferenceProvider>());
        services.AddSingleton<INotificationDeliveryAdapter, MobilePushNotificationDeliveryAdapter>();
        services.AddSingleton<INotificationDeliveryAdapter, WebNotificationDeliveryAdapter>();
        services.AddSingleton<INotificationRetryPolicy, ConfiguredNotificationRetryPolicy>();
        if (string.Equals(options.Events.Mode, "RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<RabbitMqRecommendationNotificationEventPublisher>();
            services.AddSingleton<IRecommendationEventPublisher>(provider =>
                provider.GetRequiredService<RabbitMqRecommendationNotificationEventPublisher>());
            services.AddSingleton<INotificationEventPublisher>(provider =>
                provider.GetRequiredService<RabbitMqRecommendationNotificationEventPublisher>());
        }
        else
        {
            services.AddSingleton<InMemoryNotificationEventPublisher>();
            services.AddSingleton<INotificationEventPublisher>(provider =>
                provider.GetRequiredService<InMemoryNotificationEventPublisher>());
            services.AddSingleton<InMemoryRecommendationEventPublisher>();
            services.AddSingleton<IRecommendationEventPublisher>(provider =>
                provider.GetRequiredService<InMemoryRecommendationEventPublisher>());
        }

        services.AddSingleton<RecommendationNotificationMessageHandler>();
        services.AddHostedService<RecommendationNotificationEventConsumer>();
        return services;
    }
}
