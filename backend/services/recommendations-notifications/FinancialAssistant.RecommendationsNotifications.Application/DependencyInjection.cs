using FinancialAssistant.RecommendationsNotifications.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.RecommendationsNotifications.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddRecommendationNotificationApplication(
        this IServiceCollection services)
    {
        services.AddSingleton<RecommendationGenerator>();
        services.AddSingleton<NotificationTemplateCatalog>();
        services.AddSingleton<IRecommendationWordingProvider, DeterministicRecommendationWordingProvider>();
        services.AddSingleton<RecommendationService>();
        services.AddSingleton<NotificationPreparationService>();
        return services;
    }
}
