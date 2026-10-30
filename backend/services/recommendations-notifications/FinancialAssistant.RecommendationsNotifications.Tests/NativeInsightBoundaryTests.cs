using System.Text.Json;
using FinancialAssistant.RecommendationsNotifications.Infrastructure;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Messaging;
using RabbitMQ.Client;
using Xunit;

namespace FinancialAssistant.RecommendationsNotifications.Tests;

public sealed class NativeInsightBoundaryTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
    private static IntegrationEventEnvelope<AnalyticsUpdatedV1> Analytics(string producer = "financial-assistant-analytics-service") =>
        new("synthetic-analytics", "synthetic-occurrence", AnalyticsEventTypes.AnalyticsUpdated, At, producer, 1,
            "synthetic-correlation", "synthetic-cause", new string('a', 64),
            new AnalyticsUpdatedV1("USD", DateOnly.FromDateTime(At.UtcDateTime), 100m, 50m, null, 0m, null, At));

    [Fact]
    public async Task MissingOrderCannotReachDependencies()
    {
        var handler = new NativeInsightFinancialHandler(null!, null!, null!);
        await Assert.ThrowsAsync<NativeDeliveryRejectedException>(() => handler.HandleAsync(Delivery(Analytics(), "analytics", null), default));
    }

    [Theory]
    [InlineData("profile", "financial-assistant-analytics-service", 1)]
    [InlineData("analytics", "analytics-service", 1)]
    [InlineData("analytics", "financial-assistant-analytics-service", 2)]
    public async Task ForeignAnalyticsAuthorityOrSchemaIsRejectedBeforeSettingsIO(string broker, string producer, int schema)
    {
        var handler = new NativeInsightFinancialHandler(null!, null!, null!);
        await Assert.ThrowsAsync<NativeDeliveryRejectedException>(() => handler.HandleAsync(Delivery(Analytics(producer), broker, wireSchema: schema), default));
    }

    [Theory]
    [InlineData("analytics", "financial-score-service")]
    [InlineData("financial-score", "financial-assistant-score-service")]
    public async Task ScoreRequiresItsExactBrokerAndExistingBodyProducer(string broker, string producer)
    {
        var source = new IntegrationEventEnvelope<ScoreCalculatedV1>("synthetic-score", "synthetic-occurrence", FinancialScoreEventTypes.ScoreCalculated,
            At, producer, 1, "synthetic-correlation", "synthetic-cause", new string('a', 64),
            new ScoreCalculatedV1("synthetic-calculation", "USD", 50, "financial-score-v2", [], At));
        await Assert.ThrowsAsync<NativeDeliveryRejectedException>(() => new NativeInsightFinancialHandler(null!, null!, null!)
            .HandleAsync(Delivery(source, broker), default));
    }

    [Theory]
    [InlineData("profile", "financial-assistant-recommendations-service")]
    [InlineData("recommendations-notifications", "recommendations-notifications-service")]
    public async Task PrivatePreparationRequiresOwnedPublisherIdentity(string broker, string producer)
    {
        var source = new IntegrationEventEnvelope<RecommendationGeneratedV1>("synthetic-recommendation", "synthetic-occurrence",
            RecommendationEventTypes.RecommendationGenerated, At, producer, 1, "synthetic-correlation", "synthetic-cause", new string('a', 64),
            new RecommendationGeneratedV1("synthetic-item", "USD", "synthetic-code", "low", "Synthetic", "Synthetic", [], At));
        await Assert.ThrowsAsync<NativeDeliveryRejectedException>(() => new NativeRecommendationPreparationHandler(null!)
            .HandleAsync(Delivery(source, broker), default));
    }

    private static NativeDelivery Delivery<T>(IntegrationEventEnvelope<T> source, string broker, long? sequence = 7, int? wireSchema = null)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var body = JsonSerializer.SerializeToNode(source, json)!;
        if (wireSchema is { } schema) { body["schemaVersion"] = schema; }
        var binding = NativeBinding.Event(source.EventType, "analytics", "financial-score", "profile", "recommendations-notifications");
        var subscription = new NativeSubscription("recommendations-notifications", "fixture", new[] { binding });
        return NativeDelivery.Read(subscription, NativePublication.EventsExchange, source.EventType, new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = source.EventId,
            Type = source.EventType,
            UserId = "fa-" + broker,
            Timestamp = new AmqpTimestamp(At.ToUnixTimeSeconds()),
            Headers = sequence is { } number ? new Dictionary<string, object?> { ["fa-source-sequence"] = number } : null
        }, JsonSerializer.SerializeToUtf8Bytes(body, json));
    }
}
