using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Shared.Messaging;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinancialAssistant.Audit.Infrastructure;

public static class NativeAuditMessaging
{
    public static void Add(IServiceCollection services, IConfiguration configuration, AuditOptions options)
    {
        if (!configuration.GetValue<bool>("Audit:Messaging:Enabled")) { return; }
        var connection = configuration.GetValue<string>("Audit:Messaging:ConnectionString")
            ?? throw new InvalidOperationException("Native Audit messaging configuration is missing.");
        var settings = new NativeBrokerSettings("audit", connection);
        var producers = options.AllowedProducers.Select(producer =>
            producer.EndsWith("-service", StringComparison.Ordinal) ? producer[..^8] :
                throw new InvalidOperationException("Native Audit producers must use canonical service identities.")).ToArray();
        var subscription = new NativeSubscription("audit", "events", new[] { NativeBinding.Event(AuditEventTypes.Recorded, producers) });
        services.AddSingleton<NativeAuditEventHandler>();
        services.AddSingleton<IHostedService>(provider =>
        {
            var consumer = new NativeRabbitConsumer(settings, subscription);
            var handler = provider.GetRequiredService<NativeAuditEventHandler>();
            return new NativeBrokerWorker(token => consumer.RunAsync(handler.HandleAsync, token),
                provider.GetService<TimeProvider>() ?? TimeProvider.System, provider.GetRequiredService<ILogger<NativeBrokerWorker>>());
        });
    }
}

public sealed class NativeAuditEventHandler(IAuditEventConsumer consumer)
{
    public async Task HandleAsync(NativeDelivery delivery, CancellationToken token)
    {
        var source = delivery.Deserialize<FinancialAssistant.Shared.Contracts.Events.IntegrationEventEnvelope<AuditEventV1>>();
        delivery.EnsureWireIdentity(source.EventId, source.EventType, source.OccurredAtUtc);
        if (source.Producer != delivery.PublisherUser[3..] + "-service") { throw new NativeDeliveryRejectedException(); }
        try { await consumer.ConsumeAsync(source, token); }
        catch (Exception exception) when (exception is ArgumentException or StorageConflictException)
        { throw new NativeDeliveryRejectedException(); }
    }
}
