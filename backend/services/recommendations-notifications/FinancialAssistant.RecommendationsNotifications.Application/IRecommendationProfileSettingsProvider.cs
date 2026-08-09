using FinancialAssistant.RecommendationsNotifications.Domain;

namespace FinancialAssistant.RecommendationsNotifications.Application;

public interface IRecommendationProfileSettingsProvider
{
    Task<RecommendationProfileSettings> GetAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken);
}
