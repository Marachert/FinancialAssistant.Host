using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.RecommendationsNotifications.Application;

public sealed class NotificationPreparationService
{
    private readonly IRecommendationNotificationStore store;
    private readonly NotificationTemplateCatalog templates;
    private readonly INotificationEventPublisher publisher;
    private readonly INotificationPreferenceProvider? preferenceProvider;

    public NotificationPreparationService(
        IRecommendationNotificationStore store,
        NotificationTemplateCatalog templates,
        INotificationEventPublisher publisher,
        INotificationPreferenceProvider? preferenceProvider = null)
    {
        this.store = store;
        this.templates = templates;
        this.publisher = publisher;
        this.preferenceProvider = preferenceProvider;
    }

    public async Task<IReadOnlyList<PreparedNotification>> ProcessAsync(
        IntegrationEventEnvelope<RecommendationGeneratedV1> envelope,
        CancellationToken cancellationToken)
    {
        Validate(envelope);
        var payload = envelope.Payload;
        var recommendation = new FinancialRecommendation(
            payload.RecommendationId,
            envelope.UserIdHash!,
            NormalizeCurrency(payload.Currency),
            payload.Code,
            payload.Severity,
            payload.Title,
            payload.Body,
            payload.Facts.Select(item => new RecommendationFact(item.Code, item.Value)).ToArray(),
            envelope.EventId,
            payload.GeneratedAtUtc.ToUniversalTime(),
            RecommendationStatuses.Active,
            payload.GeneratedAtUtc.ToUniversalTime());
        var preferences = preferenceProvider is null
            ? NotificationPreferences.AllEnabled
            : await preferenceProvider.GetAsync(
                recommendation.UserIdHash,
                cancellationToken);
        var prepared = new[]
        {
            NotificationChannels.Push,
            NotificationChannels.Web
        }
            .Where(channel => preferences.IsEnabled(
                channel,
                NotificationTriggerCodes.RecommendationAvailable))
            .Select(channel => templates.Prepare(
                recommendation,
                channel,
                envelope.EventId,
                envelope.OccurredAtUtc))
            .ToArray();
        var accepted = await store.SaveNotificationsIfNewAsync(prepared, cancellationToken);
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
                    envelope.CorrelationId,
                    envelope.EventId,
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

    public Task<IReadOnlyList<PreparedNotification>> GetAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken) =>
        store.GetNotificationsAsync(
            NormalizeRequired(userIdHash),
            NormalizeCurrency(currency),
            cancellationToken);

    public Task<PreparedNotification?> UpdateStatusAsync(
        string userIdHash,
        string notificationId,
        string deliveryStatus,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = NormalizeRequired(deliveryStatus).ToLowerInvariant();
        if (!NotificationDeliveryStatuses.IsTerminal(normalizedStatus) || changedAtUtc == default)
        {
            throw new ArgumentException("A terminal delivery status and timestamp are required.");
        }

        return store.UpdateNotificationStatusAsync(
            NormalizeRequired(userIdHash),
            NormalizeRequired(notificationId),
            normalizedStatus,
            changedAtUtc.ToUniversalTime(),
            cancellationToken);
    }

    private static void Validate(IntegrationEventEnvelope<RecommendationGeneratedV1> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var payload = envelope.Payload;
        if (envelope.EventType != RecommendationEventTypes.RecommendationGenerated ||
            envelope.SchemaVersion != RecommendationEventTypes.SchemaVersion ||
            string.IsNullOrWhiteSpace(envelope.UserIdHash) ||
            string.IsNullOrWhiteSpace(payload.RecommendationId) ||
            string.IsNullOrWhiteSpace(payload.Code) ||
            string.IsNullOrWhiteSpace(payload.Severity) ||
            string.IsNullOrWhiteSpace(payload.Title) ||
            string.IsNullOrWhiteSpace(payload.Body) ||
            payload.GeneratedAtUtc == default)
        {
            throw new ArgumentException("Recommendation event is invalid.", nameof(envelope));
        }
    }

    private static string NormalizeCurrency(string value)
    {
        var currency = NormalizeRequired(value).ToUpperInvariant();
        return currency.Length == 3
            ? currency
            : throw new ArgumentException("Currency must use a three-letter code.", nameof(value));
    }

    private static string NormalizeRequired(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", nameof(value))
            : value.Trim();
}
