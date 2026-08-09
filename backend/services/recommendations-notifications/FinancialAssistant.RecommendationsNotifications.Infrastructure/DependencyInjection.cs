using FinancialAssistant.RecommendationsNotifications.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRecommendationNotificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RecommendationNotificationServiceOptions>(
            configuration.GetSection(RecommendationNotificationServiceOptions.SectionName));
        var options = configuration
            .GetSection(RecommendationNotificationServiceOptions.SectionName)
            .Get<RecommendationNotificationServiceOptions>() ??
            new RecommendationNotificationServiceOptions();

        services.AddSingleton<InMemoryRecommendationNotificationStore>();
        services.AddSingleton<IRecommendationNotificationStore>(provider =>
            provider.GetRequiredService<InMemoryRecommendationNotificationStore>());
        services.AddSingleton<InMemoryRecommendationProfileSettingsProvider>();
        services.AddSingleton<IRecommendationProfileSettingsProvider>(provider =>
            provider.GetRequiredService<InMemoryRecommendationProfileSettingsProvider>());
        services.AddSingleton<InMemoryNotificationPreferenceProvider>();
        services.AddSingleton<INotificationPreferenceProvider>(provider =>
            provider.GetRequiredService<InMemoryNotificationPreferenceProvider>());
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
