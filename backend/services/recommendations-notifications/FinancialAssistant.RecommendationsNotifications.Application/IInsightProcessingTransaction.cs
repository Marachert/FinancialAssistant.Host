using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.RecommendationsNotifications.Application;

public interface IInsightProcessingTransaction
{
    Task<IReadOnlyList<FinancialRecommendation>> AnalyticsAsync(IntegrationEventEnvelope<AnalyticsUpdatedV1> source,
        Func<CancellationToken, Task<IReadOnlyList<FinancialRecommendation>>> operation, CancellationToken token);
    Task<IReadOnlyList<FinancialRecommendation>> ScoreAsync(IntegrationEventEnvelope<ScoreCalculatedV1> source,
        Func<CancellationToken, Task<IReadOnlyList<FinancialRecommendation>>> operation, CancellationToken token);
    Task<IReadOnlyList<PreparedNotification>> PrepareAsync(IntegrationEventEnvelope<RecommendationGeneratedV1> source,
        Func<CancellationToken, Task<IReadOnlyList<PreparedNotification>>> operation, CancellationToken token);
    Task<IReadOnlyList<PreparedNotification>> TriggerAsync(NotificationTriggerFacts source,
        Func<CancellationToken, Task<IReadOnlyList<PreparedNotification>>> operation, CancellationToken token);
}
