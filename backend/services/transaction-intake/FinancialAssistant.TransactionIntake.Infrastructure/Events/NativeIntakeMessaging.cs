using System.Text.Json;
using FinancialAssistant.Shared.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Events;

internal static class NativeIntakeMessaging
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static void Add(IServiceCollection services, IConfiguration? configuration)
    {
        if (configuration?.GetValue<bool>("TransactionIntake:Messaging:Enabled") != true) { return; }
        var connection = configuration.GetValue<string>("TransactionIntake:Messaging:ConnectionString")
            ?? throw new InvalidOperationException("Native Intake messaging configuration is missing.");
        var settings = new NativeBrokerSettings("transaction-intake", connection);
        services.AddSingleton<NativeRabbitPublisher>(_ => new(settings));
        services.AddSingleton<IHostedService>(provider =>
        {
            var outbox = provider.GetRequiredService<PostgresIntakeOutbox>();
            var clock = provider.GetService<TimeProvider>() ?? TimeProvider.System;
            var dispatcher = new NativeOutboxDispatcher(provider.GetRequiredService<NativeRabbitPublisher>(), async token =>
            {
                var pending = await outbox.ReadPendingAsync(token);
                // Draft-created intents retain their independent pending state; this
                // pump must neither discard them nor claim their delivery is qualified.
                return pending.Where(message => message.Kind == "confirmed").Select(message =>
                {
                    var source = message.Confirmation ?? throw new InvalidOperationException("Missing confirmed Intake intent.");
                    var recipient = source.TransactionType switch
                    { "income" => "income", "expense" => "expense", _ => throw new InvalidOperationException("Invalid confirmation recipient.") };
                    var publication = new NativePublication(exchange: NativePublication.CommandsExchange,
                        routingKey: recipient + ".transaction-confirmed.v1", eventType: source.EventType, eventId: source.EventId,
                        occurredAtUtc: source.ConfirmedAtUtc, body: JsonSerializer.SerializeToUtf8Bytes(source, Json));
                    return new NativeOutboxWork(new[] { publication }, ct => outbox.MarkPublishedAsync(message, clock.GetUtcNow(), ct));
                }).ToArray();
            });
            return new NativeBrokerWorker(async token => { await dispatcher.DispatchOnceAsync(token); },
                clock, provider.GetRequiredService<ILogger<NativeBrokerWorker>>());
        });
    }
}
