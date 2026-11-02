using System.Text.Json;
using FinancialAssistant.Profile.Client;
using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Infrastructure.Persistence;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Messaging;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public static class NativeInsightMessaging
{
    private const string Owner = "recommendations-notifications";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static void Add(IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("RecommendationsNotifications:Messaging:Enabled")) { return; }
        var connection = configuration["RecommendationsNotifications:Messaging:ConnectionString"]
            ?? throw new InvalidOperationException("Native insight messaging configuration is missing.");
        var secret = configuration["RecommendationsNotifications:Profile:SharedSecret"]
            ?? throw new InvalidOperationException("Native insight Profile reader configuration is missing.");
        var address = configuration["RecommendationsNotifications:Profile:BaseAddress"] ?? "http://127.0.0.1:5102";
        var settings = new NativeBrokerSettings(Owner, connection);
        var financial = new NativeSubscription(Owner, "insights", new[]
        {
            NativeBinding.Event(AnalyticsEventTypes.AnalyticsUpdated, "analytics"),
            NativeBinding.Event(FinancialScoreEventTypes.ScoreCalculated, "financial-score")
        });
        var handoff = NativeBinding.Command(Owner, "recommendation-generated", RecommendationEventTypes.RecommendationGenerated, Owner);
        var prepared = new NativeSubscription(Owner, "prepared", new[] { handoff });
        services.AddSingleton(provider => new NativeProfileSettingsClient(Owner, address, secret,
            provider.GetService<TimeProvider>() ?? TimeProvider.System));
        services.AddSingleton<NativeInsightProfileSettingsLoader>();
        services.AddSingleton<NativeInsightFinancialHandler>();
        services.AddSingleton<NativeRecommendationPreparationHandler>();
        services.AddSingleton<IHostedService>(provider =>
        {
            var consumer = new NativeRabbitConsumer(settings, financial);
            var handler = provider.GetRequiredService<NativeInsightFinancialHandler>();
            return new NativeBrokerWorker(token => consumer.RunAsync(handler.HandleAsync, token),
                provider.GetService<TimeProvider>() ?? TimeProvider.System, provider.GetRequiredService<ILogger<NativeBrokerWorker>>());
        });
        services.AddSingleton<IHostedService>(provider =>
        {
            var consumer = new NativeRabbitConsumer(settings, prepared);
            var handler = provider.GetRequiredService<NativeRecommendationPreparationHandler>();
            return new NativeBrokerWorker(token => consumer.RunAsync(handler.HandleAsync, token),
                provider.GetService<TimeProvider>() ?? TimeProvider.System, provider.GetRequiredService<ILogger<NativeBrokerWorker>>());
        });
        services.AddSingleton<NativeRabbitPublisher>(_ => new(settings));
        services.AddSingleton<IHostedService>(provider =>
        {
            var outbox = provider.GetRequiredService<PostgresInsightEventPublisher>();
            var clock = provider.GetService<TimeProvider>() ?? TimeProvider.System;
            var dispatcher = new NativeOutboxDispatcher(provider.GetRequiredService<NativeRabbitPublisher>(), async token =>
            {
                var pending = await outbox.ReadRecommendationsBatchAsync(token);
                return pending.Select(message => new NativeOutboxWork(new[] { new NativePublication(
                    exchange: NativePublication.CommandsExchange, routingKey: handoff.RoutingKey, eventType: message.Event.EventType,
                    eventId: message.Event.EventId, occurredAtUtc: message.Event.OccurredAtUtc,
                    body: JsonSerializer.SerializeToUtf8Bytes(message.Event, Json)) },
                    async ct => { await outbox.MarkPublishedAsync(message, clock.GetUtcNow(), ct); })).ToArray();
            });
            // Prepared notification intents remain held; no provider is invoked or falsely acknowledged.
            return new NativeBrokerWorker(async token => { await dispatcher.DispatchOnceAsync(token); },
                clock, provider.GetRequiredService<ILogger<NativeBrokerWorker>>());
        });
    }
}

public sealed class NativeInsightFinancialHandler(RecommendationService service, InsightPersistenceContext context,
    NativeInsightProfileSettingsLoader profiles)
{
    public async Task HandleAsync(NativeDelivery delivery, CancellationToken token)
    {
        if (delivery.SourceSequence is not { } sequence) { throw new NativeDeliveryRejectedException(); }
        try
        {
            switch (delivery.EventType)
            {
                case AnalyticsEventTypes.AnalyticsUpdated:
                    var analytics = delivery.Deserialize<IntegrationEventEnvelope<AnalyticsUpdatedV1>>();
                    delivery.EnsureWireIdentity(analytics.EventId, analytics.EventType, analytics.OccurredAtUtc);
                    if (delivery.PublisherUser != "fa-analytics" || analytics.Producer != "financial-assistant-analytics-service" ||
                        analytics.SchemaVersion != 1 || analytics.Payload is null) { throw new NativeDeliveryRejectedException(); }
                    await profiles.RefreshAsync(analytics.UserIdHash!, analytics.Payload.Currency, token);
                    await context.ExecuteNativeOrderedAsync("analytics", sequence, ct => service.ProcessAnalyticsAsync(analytics, ct), token);
                    break;
                case FinancialScoreEventTypes.ScoreCalculated:
                    var score = delivery.Deserialize<IntegrationEventEnvelope<ScoreCalculatedV1>>();
                    delivery.EnsureWireIdentity(score.EventId, score.EventType, score.OccurredAtUtc);
                    if (delivery.PublisherUser != "fa-financial-score" || score.Producer != "financial-score-service" ||
                        score.SchemaVersion != 1 || score.Payload is null) { throw new NativeDeliveryRejectedException(); }
                    await profiles.RefreshAsync(score.UserIdHash!, score.Payload.Currency, token);
                    await context.ExecuteNativeOrderedAsync("score", sequence, ct => service.ProcessScoreAsync(score, ct), token);
                    break;
                default: throw new NativeDeliveryRejectedException();
            }
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or StorageConflictException)
        { throw new NativeDeliveryRejectedException(); }
    }
}

public sealed class NativeRecommendationPreparationHandler(NotificationPreparationService service)
{
    public async Task HandleAsync(NativeDelivery delivery, CancellationToken token)
    {
        var source = delivery.Deserialize<IntegrationEventEnvelope<RecommendationGeneratedV1>>();
        delivery.EnsureWireIdentity(source.EventId, source.EventType, source.OccurredAtUtc);
        if (delivery.PublisherUser != "fa-recommendations-notifications" || source.Producer != "financial-assistant-recommendations-service")
        { throw new NativeDeliveryRejectedException(); }
        try { await service.ProcessAsync(source, token); }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException or StorageConflictException)
        { throw new NativeDeliveryRejectedException(); }
    }
}
