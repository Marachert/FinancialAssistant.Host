using System.Text.Json;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Income.Infrastructure.Persistence;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Messaging;
using FinancialAssistant.TransactionIntake.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinancialAssistant.Income.Infrastructure;

internal static class NativeIncomeMessaging
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static void Add(IServiceCollection services, IConfiguration? configuration)
    {
        if (configuration?.GetValue<bool>("Income:Messaging:Enabled") != true) { return; }
        var connection = configuration.GetValue<string>("Income:Messaging:ConnectionString")
            ?? throw new InvalidOperationException("Native Income messaging configuration is missing.");
        var settings = new NativeBrokerSettings("income", connection);
        services.AddSingleton<NativeRabbitPublisher>(_ => new(settings));
        services.AddSingleton<NativeIncomeConfirmationHandler>();
        services.AddSingleton<IHostedService>(provider =>
        {
            var publisher = provider.GetRequiredService<NativeRabbitPublisher>();
            var outbox = provider.GetRequiredService<PostgresIncomeRecordEventPublisher>();
            var clock = provider.GetService<TimeProvider>() ?? TimeProvider.System;
            var dispatcher = new NativeOutboxDispatcher(publisher, async token =>
            {
                var pending = await outbox.ReadPendingAsync(token);
                return pending.Select(message => new NativeOutboxWork(
                    new[] { Publication(message.Event), Publication(FinancialCommitAudit.Create(message.Event)) },
                    ct => outbox.MarkPublishedAsync(message, clock.GetUtcNow(), ct))).ToArray();
            });
            return new NativeBrokerWorker(async token => { await dispatcher.DispatchOnceAsync(token); },
                clock, provider.GetRequiredService<ILogger<NativeBrokerWorker>>());
        });
        services.AddSingleton<NativeRabbitConsumer>(_ =>
        {
            var subscription = new NativeSubscription("income", "confirmed",
                new[] { NativeBinding.Command("income", "transaction-confirmed", TransactionConfirmedIntegrationEvent.Name, "transaction-intake") });
            return new NativeRabbitConsumer(settings, subscription);
        });
        services.AddSingleton<IHostedService>(provider =>
        {
            var consumer = provider.GetRequiredService<NativeRabbitConsumer>();
            var handler = provider.GetRequiredService<NativeIncomeConfirmationHandler>();
            return new NativeBrokerWorker(token => consumer.RunAsync(handler.HandleAsync, token),
                provider.GetService<TimeProvider>() ?? TimeProvider.System, provider.GetRequiredService<ILogger<NativeBrokerWorker>>());
        });
    }

    private static NativePublication Publication<T>(IntegrationEventEnvelope<T> envelope) => new(
        exchange: NativePublication.EventsExchange, routingKey: envelope.EventType, eventType: envelope.EventType,
        eventId: envelope.EventId, occurredAtUtc: envelope.OccurredAtUtc, body: JsonSerializer.SerializeToUtf8Bytes(envelope, Json));
}

public sealed class NativeIncomeConfirmationHandler(ITransactionConfirmedConsumer consumer)
{
    public async Task HandleAsync(NativeDelivery delivery, CancellationToken token)
    {
        var source = delivery.Deserialize<TransactionConfirmedIntegrationEvent>();
        delivery.EnsureWireIdentity(source.EventId, source.EventType, source.ConfirmedAtUtc);
        if (delivery.PublisherUser != "fa-transaction-intake" || source.TransactionType != "income")
        { throw new NativeDeliveryRejectedException(); }
        try { await consumer.ConsumeAsync(source, token); }
        catch (Exception exception) when (exception is ArgumentException or FinancialConfirmationConflictException)
        { throw new NativeDeliveryRejectedException(); }
    }
}
