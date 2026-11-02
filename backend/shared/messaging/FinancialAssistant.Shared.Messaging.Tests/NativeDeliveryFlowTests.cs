using System.Text;
using RabbitMQ.Client;
using Xunit;

namespace FinancialAssistant.Shared.Messaging.Tests;

public sealed class NativeDeliveryFlowTests
{
    private static NativeDelivery Delivery(int attempt = 0)
    {
        var subscription = new NativeSubscription("analytics", "financial-events", new[] { NativeBinding.Event("income.created.v1", "income") });
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = "synthetic-commit",
            Type = "income.created.v1",
            UserId = attempt == 0 ? "fa-income" : subscription.BrokerUser
        };
        if (attempt > 0)
        {
            properties.Headers = new Dictionary<string, object?>
            {
                [NativeDelivery.AttemptHeader] = attempt,
                [NativeDelivery.TargetHeader] = Encoding.UTF8.GetBytes(subscription.Queue),
                [NativeDelivery.OriginHeader] = Encoding.UTF8.GetBytes("fa-income")
            };
        }
        return NativeDelivery.Read(subscription, attempt == 0 ? "fa.events" : NativeSubscription.RetryReturnExchange,
            attempt == 0 ? "income.created.v1" : subscription.Queue, properties,
            Encoding.UTF8.GetBytes("{\"eventId\":\"synthetic-commit\",\"eventType\":\"income.created.v1\"}"));
    }

    [Fact]
    public async Task OwnedCommitPrecedesAcknowledgement()
    {
        var settlement = new Settlement();
        await NativeDeliveryFlow.ProcessAsync(Delivery(), (_, _) => { settlement.Calls.Add("commit"); return Task.CompletedTask; }, settlement, default);
        Assert.Equal(new[] { "commit", "ack" }, settlement.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransientStorageFailureOrUnrelatedCancellationRetriesBeforeAck(bool cancellation)
    {
        var settlement = new Settlement();
        await NativeDeliveryFlow.ProcessAsync(Delivery(), (_, _) =>
        { if (cancellation) { throw new OperationCanceledException(); } throw new IOException(); }, settlement, default);
        Assert.Equal(new[] { "retry-confirmed", "ack" }, settlement.Calls);
    }

    [Fact]
    public async Task FailedRetryPublicationNeverAcknowledgesOrRejectsTheOriginal()
    {
        var settlement = new Settlement { RetryFails = true };
        await Assert.ThrowsAsync<IOException>(() => NativeDeliveryFlow.ProcessAsync(Delivery(), (_, _) => throw new IOException(), settlement, default));
        Assert.Equal(new[] { "retry-failed" }, settlement.Calls);
    }

    [Fact]
    public async Task UncertainAckDoesNotRepublishAnAlreadyCommittedMessage()
    {
        var settlement = new Settlement { AckFails = true };
        await Assert.ThrowsAsync<IOException>(() => NativeDeliveryFlow.ProcessAsync(Delivery(), (_, _) => Task.CompletedTask, settlement, default));
        Assert.Equal(new[] { "ack" }, settlement.Calls);
    }

    [Fact]
    public async Task InvalidTypedPayloadIsRejectedWithoutRetry()
    {
        var settlement = new Settlement();
        await NativeDeliveryFlow.ProcessAsync(Delivery(), (_, _) => throw new NativeDeliveryRejectedException(), settlement, default);
        Assert.Equal(new[] { "reject" }, settlement.Calls);
    }

    [Fact]
    public async Task ThirdRetryFailureIsTerminal()
    {
        var settlement = new Settlement();
        await NativeDeliveryFlow.ProcessAsync(Delivery(3), (_, _) => throw new IOException(), settlement, default);
        Assert.Equal(new[] { "reject" }, settlement.Calls);
    }

    [Fact]
    public async Task ShutdownLeavesTheMessageUnsettledForBrokerRedelivery()
    {
        using var stopping = new CancellationTokenSource(); var settlement = new Settlement();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NativeDeliveryFlow.ProcessAsync(Delivery(), (_, token) =>
        { stopping.Cancel(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; }, settlement, stopping.Token));
        Assert.Empty(settlement.Calls);
    }

    [Fact]
    public void ConsumerCannotUseAnotherServicesCredentials()
    {
        var settings = new NativeBrokerSettings("income", "amqp://fa-income:" + new string('s', 48) + "@127.0.0.1:5679/fa-synthetic");
        var subscription = new NativeSubscription("analytics", "financial-events", new[] { NativeBinding.Event("income.created.v1", "income") });
        Assert.Throws<ArgumentException>(() => new NativeRabbitConsumer(settings, subscription));
    }

    private sealed class Settlement : INativeDeliverySettlement
    {
        internal List<string> Calls { get; } = new();
        internal bool RetryFails { get; init; }
        internal bool AckFails { get; init; }
        public Task PublishRetryAsync(NativeDelivery delivery, CancellationToken token)
        {
            Calls.Add(RetryFails ? "retry-failed" : "retry-confirmed");
            return RetryFails ? Task.FromException(new IOException()) : Task.CompletedTask;
        }
        public Task AcknowledgeAsync(CancellationToken token)
        {
            Calls.Add("ack"); return AckFails ? Task.FromException(new IOException()) : Task.CompletedTask;
        }
        public Task RejectAsync(CancellationToken token) { Calls.Add("reject"); return Task.CompletedTask; }
    }
}
