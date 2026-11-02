using System.Text.Json;
using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Infrastructure.Persistence;
using FinancialAssistant.Profile.Client;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinancialAssistant.FinancialScore.Infrastructure;

public static class NativeFinancialScoreMessaging
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static void Add(IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>("FinancialScore:Messaging:Enabled")) { return; }
        var connection = configuration.GetValue<string>("FinancialScore:Messaging:ConnectionString")
            ?? throw new InvalidOperationException("Native FinancialScore messaging configuration is missing.");
        var settings = new NativeBrokerSettings("financial-score", connection);
        var profileSecret = configuration.GetValue<string>("FinancialScore:Profile:SharedSecret")
            ?? throw new InvalidOperationException("Native Score Profile reader configuration is missing.");
        var profileAddress = configuration.GetValue<string>("FinancialScore:Profile:BaseAddress") ?? "http://127.0.0.1:5102";
        services.AddSingleton(provider => new NativeProfileSettingsClient("financial-score", profileAddress, profileSecret,
            provider.GetService<TimeProvider>() ?? TimeProvider.System));
        services.AddSingleton<NativeScoreProfileSettingsLoader>();
        var subscription = new NativeSubscription("financial-score", "financial", new[]
        {
            NativeBinding.Event(FinancialRecordEventTypes.IncomeCreated, "income"),
            NativeBinding.Event(FinancialRecordEventTypes.IncomeUpdated, "income"),
            NativeBinding.Event(FinancialRecordEventTypes.IncomeArchived, "income"),
            NativeBinding.Event(FinancialRecordEventTypes.IncomeRestored, "income"),
            NativeBinding.Event(FinancialRecordEventTypes.ExpenseCreated, "expense"),
            NativeBinding.Event(FinancialRecordEventTypes.ExpenseUpdated, "expense"),
            NativeBinding.Event(FinancialRecordEventTypes.ExpenseArchived, "expense"),
            NativeBinding.Event(FinancialRecordEventTypes.ExpenseRestored, "expense")
        });
        services.AddSingleton<NativeFinancialScoreFinancialHandler>();
        services.AddSingleton<IHostedService>(provider =>
        {
            var consumer = new NativeRabbitConsumer(settings, subscription);
            var handler = provider.GetRequiredService<NativeFinancialScoreFinancialHandler>();
            return new NativeBrokerWorker(token => consumer.RunAsync(handler.HandleAsync, token),
                provider.GetService<TimeProvider>() ?? TimeProvider.System, provider.GetRequiredService<ILogger<NativeBrokerWorker>>());
        });
        services.AddSingleton<NativeRabbitPublisher>(_ => new(settings));
        services.AddSingleton<IHostedService>(provider =>
        {
            var outbox = provider.GetRequiredService<PostgresFinancialScoreEventPublisher>();
            var clock = provider.GetService<TimeProvider>() ?? TimeProvider.System;
            var dispatcher = new NativeOutboxDispatcher(provider.GetRequiredService<NativeRabbitPublisher>(), async token =>
            {
                var pending = await outbox.ReadPendingBatchAsync(token);
                return pending.Select(message => new NativeOutboxWork(new[] { new NativePublication(
                    exchange: NativePublication.EventsExchange, routingKey: message.Event.EventType, eventType: message.Event.EventType,
                    eventId: message.Event.EventId, occurredAtUtc: message.Event.OccurredAtUtc,
                    body: JsonSerializer.SerializeToUtf8Bytes(message.Event, Json), sourceSequence: message.SourceSequence) },
                    async ct => { await outbox.MarkPublishedAsync(message, clock.GetUtcNow(), ct); })).ToArray();
            });
            return new NativeBrokerWorker(async token => { await dispatcher.DispatchOnceAsync(token); },
                clock, provider.GetRequiredService<ILogger<NativeBrokerWorker>>());
        });
    }
}

public sealed class NativeFinancialScoreFinancialHandler(FinancialScoreFinancialEventMessageHandler handler, NativeScoreProfileSettingsLoader profiles)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task HandleAsync(NativeDelivery delivery, CancellationToken token)
    {
        var source = delivery.Deserialize<IntegrationEventEnvelope<FinancialRecordChangedV1>>();
        delivery.EnsureWireIdentity(source.EventId, source.EventType, source.OccurredAtUtc);
        var expected = source.EventType.StartsWith("income.", StringComparison.Ordinal)
            ? ("fa-income", "income-service") : ("fa-expense", "expense-service");
        if (delivery.PublisherUser != expected.Item1 || source.Producer != expected.Item2)
        { throw new NativeDeliveryRejectedException(); }
        try
        {
            await profiles.RefreshAsync(source.UserIdHash!, new[] { "USD", "EUR", "GBP", "UAH", source.Payload.Currency }, token);
            await handler.HandleAsync(JsonSerializer.SerializeToUtf8Bytes(source, Json), token);
        }
        catch (Exception exception) when (exception is ArgumentException or JsonException or FormatException)
        { throw new NativeDeliveryRejectedException(); }
    }
}
