using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.RecommendationsNotifications.Application;

public sealed class NotificationTriggerService
{
    private static readonly string[] Channels =
    [
        NotificationChannels.Push,
        NotificationChannels.Web
    ];

    private readonly IRecommendationNotificationStore store;
    private readonly NotificationTriggerEvaluator evaluator;
    private readonly NotificationTemplateCatalog templates;
    private readonly INotificationEventPublisher publisher;
    private readonly INotificationPreferenceProvider? preferenceProvider;

    public NotificationTriggerService(
        IRecommendationNotificationStore store,
        NotificationTriggerEvaluator evaluator,
        NotificationTemplateCatalog templates,
        INotificationEventPublisher publisher,
        INotificationPreferenceProvider? preferenceProvider = null)
    {
        this.store = store;
        this.evaluator = evaluator;
        this.templates = templates;
        this.publisher = publisher;
        this.preferenceProvider = preferenceProvider;
    }

    public async Task<IReadOnlyList<PreparedNotification>> ProcessAsync(
        NotificationTriggerFacts facts,
        CancellationToken cancellationToken)
    {
        var candidates = evaluator.Evaluate(facts);
        var preferences = preferenceProvider is null
            ? NotificationPreferences.AllEnabled
            : await preferenceProvider.GetAsync(
                facts.UserIdHash,
                cancellationToken);
        var prepared = candidates
            .SelectMany(candidate => Channels
                .Where(channel => preferences.IsEnabled(channel, candidate.Code))
                .Select(channel => templates.Prepare(candidate, channel)))
            .ToArray();
        var accepted = await store.SaveNotificationsIfNewAsync(
            prepared,
            cancellationToken);
        foreach (var notification in accepted)
        {
            await publisher.PublishAsync(
                new IntegrationEventEnvelope<NotificationPreparedV1>(
                    $"notification-{notification.NotificationId}",
                    notification.NotificationId,
                    NotificationEventTypes.NotificationPrepared,
                    notification.PreparedAtUtc,
                    "financial-assistant-notification-service",
                    NotificationEventTypes.SchemaVersion,
                    facts.CorrelationId,
                    facts.SourceEventId,
                    notification.UserIdHash,
                    new NotificationPreparedV1(
                        notification.NotificationId,
                        notification.RecommendationId,
                        notification.Currency,
                        notification.Channel,
                        notification.TemplateCode,
                        notification.DeliveryStatus,
                        notification.PreparedAtUtc)),
                cancellationToken);
            await store.MarkNotificationPublishedAsync(
                notification.NotificationId,
                cancellationToken);
        }

        return accepted;
    }
}
