using System.Text.Json;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Audit.Infrastructure;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Messaging;
using FinancialAssistant.Shared.Persistence;
using RabbitMQ.Client;
using Xunit;

namespace FinancialAssistant.Audit.Tests;

public sealed class NativeAuditEventHandlerTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
    private static IntegrationEventEnvelope<AuditEventV1> Source(string producer = "income-service") => new(
        "synthetic-audit-event", "synthetic-audit-occurrence", AuditEventTypes.Recorded, At, producer, 1,
        "synthetic-correlation", "synthetic-financial-cause", new string('a', 64),
        new AuditEventV1(AuditDomains.Business, AuditActions.IncomeCreated, AuditOutcomes.Succeeded,
            AuditResourceTypes.Income, null, AuditRetentionClasses.Regulatory, AuditActorTypes.System, null));

    private static NativeDelivery Delivery(IntegrationEventEnvelope<AuditEventV1> source)
    {
        var binding = NativeBinding.Event(AuditEventTypes.Recorded, "income", "expense");
        var subscription = new NativeSubscription("audit", "events", new[] { binding });
        return NativeDelivery.Read(subscription, NativePublication.EventsExchange, binding.RoutingKey, new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = source.EventId,
            Type = source.EventType,
            UserId = "fa-income",
            Timestamp = new AmqpTimestamp(At.ToUnixTimeSeconds())
        }, JsonSerializer.SerializeToUtf8Bytes(source, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public async Task AuthenticatedMetadataReachesTheOwnedConsumerUnchanged()
    {
        var consumer = new Consumer(); var source = Source();
        await new NativeAuditEventHandler(consumer).HandleAsync(Delivery(source), default);
        Assert.Equal(source, consumer.Seen); Assert.Equal(1, consumer.Calls);
    }

    [Fact]
    public async Task BrokerPublisherCannotImpersonateAnotherBodyProducer()
    {
        var consumer = new Consumer();
        await Assert.ThrowsAsync<NativeDeliveryRejectedException>(() => new NativeAuditEventHandler(consumer)
            .HandleAsync(Delivery(Source("expense-service")), default));
        Assert.Equal(0, consumer.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidMetadataAndConflictingImmutableReceiptsAreTerminal(bool conflict)
    {
        var consumer = new Consumer { Failure = conflict ? new StorageConflictException() : new ArgumentException("synthetic-invalid") };
        await Assert.ThrowsAsync<NativeDeliveryRejectedException>(() => new NativeAuditEventHandler(consumer).HandleAsync(Delivery(Source()), default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StorageUnavailabilityAndDeadlineCancellationRemainTransient(bool deadline)
    {
        var failure = deadline ? (Exception)new OperationCanceledException() : new StorageUnavailableException();
        var consumer = new Consumer { Failure = failure };
        var thrown = await Record.ExceptionAsync(() => new NativeAuditEventHandler(consumer).HandleAsync(Delivery(Source()), default));
        Assert.Same(failure, thrown);
    }

    private sealed class Consumer : IAuditEventConsumer
    {
        internal IntegrationEventEnvelope<AuditEventV1>? Seen { get; private set; }
        internal int Calls { get; private set; }
        internal Exception? Failure { get; init; }
        public Task<string> ConsumeAsync(IntegrationEventEnvelope<AuditEventV1> source, CancellationToken token)
        {
            Seen = source; Calls++;
            return Failure is null ? Task.FromResult("synthetic-audit-id") : Task.FromException<string>(Failure);
        }
    }
}
