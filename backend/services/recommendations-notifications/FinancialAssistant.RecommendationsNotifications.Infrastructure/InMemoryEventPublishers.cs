using System.Collections.Concurrent;
using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public sealed class InMemoryRecommendationEventPublisher : IRecommendationEventPublisher
{
    private readonly ConcurrentQueue<IntegrationEventEnvelope<RecommendationGeneratedV1>> published = new();
    private readonly NotificationPreparationService notificationService;

    public InMemoryRecommendationEventPublisher(
        NotificationPreparationService notificationService)
    {
        this.notificationService = notificationService;
    }

    public IReadOnlyCollection<IntegrationEventEnvelope<RecommendationGeneratedV1>> Published =>
        published.ToArray();

    public async Task PublishAsync(
        IntegrationEventEnvelope<RecommendationGeneratedV1> envelope,
        CancellationToken cancellationToken)
    {
        published.Enqueue(envelope);
        await notificationService.ProcessAsync(envelope, cancellationToken);
    }
}

public sealed class InMemoryNotificationEventPublisher : INotificationEventPublisher
{
    private readonly ConcurrentQueue<IntegrationEventEnvelope<NotificationPreparedV1>> published = new();

    public IReadOnlyCollection<IntegrationEventEnvelope<NotificationPreparedV1>> Published =>
        published.ToArray();

    public Task PublishAsync(
        IntegrationEventEnvelope<NotificationPreparedV1> envelope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        published.Enqueue(envelope);
        return Task.CompletedTask;
    }
}
