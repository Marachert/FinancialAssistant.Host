using System.Text.Json;
using FinancialAssistant.Income.Infrastructure;
using FinancialAssistant.Income.Infrastructure.Persistence;
using FinancialAssistant.Shared.Messaging;
using FinancialAssistant.TransactionIntake.Contracts;
using RabbitMQ.Client;
using Xunit;

namespace FinancialAssistant.Income.Tests;

public sealed class NativeConfirmationHandlerTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
    private static TransactionConfirmedIntegrationEvent Source(string type = "income") => new(
        "synthetic-confirmation", "synthetic-transaction", "synthetic-user", "synthetic-draft", type,
        42m, "USD", "income.synthetic", null, new DateOnly(2026, 10, 29), At, "synthetic-correlation");

    private static NativeDelivery Delivery(TransactionConfirmedIntegrationEvent source, string publisher = "transaction-intake")
    {
        var binding = NativeBinding.Command("income", "transaction-confirmed", TransactionConfirmedIntegrationEvent.Name, "transaction-intake", "profile");
        var subscription = new NativeSubscription("income", "confirmed", new[] { binding });
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = source.EventId,
            Type = source.EventType,
            UserId = "fa-" + publisher,
            Timestamp = new AmqpTimestamp(source.ConfirmedAtUtc.ToUnixTimeSeconds())
        };
        return NativeDelivery.Read(subscription, NativePublication.CommandsExchange, binding.RoutingKey, properties,
            JsonSerializer.SerializeToUtf8Bytes(source, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public async Task ValidOwnedConfirmationReachesTheTransactionalAuthorityUnchanged()
    {
        var consumer = new Consumer(); var source = Source();
        await new NativeIncomeConfirmationHandler(consumer).HandleAsync(Delivery(source), default);
        Assert.Equal(source, consumer.Seen); Assert.Equal(1, consumer.Calls);
    }

    [Fact]
    public async Task ForeignRecordFamilyOrPublisherCannotBecomeASuccessfulNoOp()
    {
        var consumer = new Consumer(); var handler = new NativeIncomeConfirmationHandler(consumer);
        await Assert.ThrowsAsync<NativeDeliveryRejectedException>(() => handler.HandleAsync(Delivery(Source("expense")), default));
        await Assert.ThrowsAsync<NativeDeliveryRejectedException>(() => handler.HandleAsync(Delivery(Source(), "profile"), default));
        Assert.Equal(0, consumer.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidFinancialStateAndConflictingReceiptsAreTerminal(bool conflict)
    {
        var consumer = new Consumer { Failure = conflict ? new FinancialConfirmationConflictException() : new ArgumentException("synthetic-invalid-state") };
        await Assert.ThrowsAsync<NativeDeliveryRejectedException>(() => new NativeIncomeConfirmationHandler(consumer).HandleAsync(Delivery(Source()), default));
    }

    [Fact]
    public async Task StorageDeadlineRemainsTransientInsteadOfBeingClassifiedAsInvalidInput()
    {
        var consumer = new Consumer { Failure = new OperationCanceledException() };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new NativeIncomeConfirmationHandler(consumer).HandleAsync(Delivery(Source()), default));
    }

    private sealed class Consumer : ITransactionConfirmedConsumer
    {
        internal TransactionConfirmedIntegrationEvent? Seen { get; private set; }
        internal int Calls { get; private set; }
        internal Exception? Failure { get; init; }
        public Task ConsumeAsync(TransactionConfirmedIntegrationEvent value, CancellationToken token)
        {
            Calls++; Seen = value;
            return Failure is null ? Task.CompletedTask : Task.FromException(Failure);
        }
    }
}
