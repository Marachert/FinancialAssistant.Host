using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.RecommendationsNotifications.Application;

public sealed record InsightApplyResult(
    bool Accepted,
    InsightSnapshot Snapshot);

public sealed record RecommendationWording(
    string Title,
    string Body);

public interface IRecommendationNotificationStore
{
    Task<InsightApplyResult> ApplyAnalyticsIfNewAsync(
        string sourceEventId,
        string userIdHash,
        string currency,
        AnalyticsInsightFacts analytics,
        CancellationToken cancellationToken);

    Task<InsightApplyResult> ApplyScoreIfNewAsync(
        string sourceEventId,
        string userIdHash,
        string currency,
        ScoreInsightFacts score,
        CancellationToken cancellationToken);

    Task MarkInsightEventCompletedAsync(
        string sourceEventId,
        string userIdHash,
        CancellationToken cancellationToken);

    Task ReplaceCurrentRecommendationsAsync(
        string userIdHash,
        string currency,
        IReadOnlyList<FinancialRecommendation> recommendations,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FinancialRecommendation>> GetRecommendationsAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken);

    Task<FinancialRecommendation?> UpdateRecommendationStatusAsync(
        string userIdHash,
        string recommendationId,
        string status,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PreparedNotification>> SaveNotificationsIfNewAsync(
        IReadOnlyList<PreparedNotification> notifications,
        CancellationToken cancellationToken);

    Task MarkNotificationPublishedAsync(
        string notificationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PreparedNotification>> GetNotificationsAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken);

    Task<PreparedNotification?> UpdateNotificationStatusAsync(
        string userIdHash,
        string notificationId,
        string deliveryStatus,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken);
}

public interface IRecommendationWordingProvider
{
    Task<RecommendationWording> CreateAsync(
        FinancialRecommendation recommendation,
        CancellationToken cancellationToken);
}

public interface IRecommendationEventPublisher
{
    Task PublishAsync(
        IntegrationEventEnvelope<RecommendationGeneratedV1> envelope,
        CancellationToken cancellationToken);
}

public interface INotificationEventPublisher
{
    Task PublishAsync(
        IntegrationEventEnvelope<NotificationPreparedV1> envelope,
        CancellationToken cancellationToken);
}
