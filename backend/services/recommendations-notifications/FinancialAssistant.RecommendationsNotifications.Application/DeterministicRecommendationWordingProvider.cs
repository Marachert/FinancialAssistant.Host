using FinancialAssistant.RecommendationsNotifications.Domain;

namespace FinancialAssistant.RecommendationsNotifications.Application;

public sealed class DeterministicRecommendationWordingProvider : IRecommendationWordingProvider
{
    public Task<RecommendationWording> CreateAsync(
        FinancialRecommendation recommendation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new RecommendationWording(
            recommendation.Title,
            recommendation.Body));
    }
}
