using System.Text.Json;
using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public sealed class RecommendationNotificationMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RecommendationService recommendationService;
    private readonly NotificationPreparationService notificationService;

    public RecommendationNotificationMessageHandler(
        RecommendationService recommendationService,
        NotificationPreparationService notificationService)
    {
        this.recommendationService = recommendationService;
        this.notificationService = notificationService;
    }

    public async Task HandleAsync(
        ReadOnlyMemory<byte> message,
        CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(message);
        if (!document.RootElement.TryGetProperty("eventType", out var eventTypeProperty))
        {
            throw new JsonException("Event type is required.");
        }

        var eventType = eventTypeProperty.GetString();
        switch (eventType)
        {
            case AnalyticsEventTypes.AnalyticsUpdated:
                await recommendationService.ProcessAnalyticsAsync(
                    Deserialize<AnalyticsUpdatedV1>(message),
                    cancellationToken);
                break;
            case FinancialScoreEventTypes.ScoreCalculated:
                await recommendationService.ProcessScoreAsync(
                    Deserialize<ScoreCalculatedV1>(message),
                    cancellationToken);
                break;
            case RecommendationEventTypes.RecommendationGenerated:
                await notificationService.ProcessAsync(
                    Deserialize<RecommendationGeneratedV1>(message),
                    cancellationToken);
                break;
            default:
                throw new ArgumentException("Unsupported insight event type.", nameof(message));
        }
    }

    private static IntegrationEventEnvelope<TPayload> Deserialize<TPayload>(
        ReadOnlyMemory<byte> message) =>
        JsonSerializer.Deserialize<IntegrationEventEnvelope<TPayload>>(
            message.Span,
            JsonOptions) ?? throw new JsonException("Event envelope is required.");
}
