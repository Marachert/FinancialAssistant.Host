using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RabbitMQ.Client;
using Xunit;

namespace FinancialAssistant.Shared.Messaging.Tests;

public sealed class NativeDeliveryTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
    private static NativeSubscription Subscription() => new("analytics", "financial-events",
        new[] { NativeBinding.Event("income.created.v1", "income") });
    private static BasicProperties Properties(string user = "fa-income") => new()
    {
        ContentType = "application/json",
        ContentEncoding = "utf-8",
        DeliveryMode = DeliveryModes.Persistent,
        MessageId = "synthetic-source",
        Type = "income.created.v1",
        UserId = user,
        Timestamp = new AmqpTimestamp(At.ToUnixTimeSeconds())
    };
    private static byte[] Body(string extra = "") => Encoding.UTF8.GetBytes("{\"eventId\":\"synthetic-source\",\"eventType\":\"income.created.v1\",\"value\":7" + extra + "}");

    [Fact]
    public void ValidatedDeliveryOwnsItsBufferAndTypedDecoderIsStrict()
    {
        var bytes = Body(); var delivery = NativeDelivery.Read(Subscription(), "fa.events", "income.created.v1", Properties(), bytes);
        bytes[0] = 0;
        Assert.Equal(7, delivery.Deserialize<Payload>().Value); Assert.Equal("fa-income", delivery.PublisherUser);
        delivery.EnsureWireIdentity("synthetic-source", "income.created.v1", At);
        Assert.Throws<NativeDeliveryRejectedException>(() => delivery.EnsureWireIdentity("synthetic-forged", delivery.EventType, At));
        Assert.Throws<NativeDeliveryRejectedException>(() => delivery.EnsureWireIdentity(delivery.EventId, delivery.EventType, At.AddSeconds(1)));
        Assert.DoesNotContain("value", delivery.ToString()); Assert.DoesNotContain("value", JsonSerializer.Serialize(delivery));
        var unknown = NativeDelivery.Read(Subscription(), "fa.events", "income.created.v1", Properties(), Body(",\"amount\":123"));
        Assert.Throws<NativeDeliveryRejectedException>(() => unknown.Deserialize<Payload>());
    }

    [Theory]
    [InlineData("fa-profile")]
    [InlineData("guest")]
    [InlineData(null)]
    public void MissingOrForeignBrokerPublisherIsTerminal(string? user)
    {
        var properties = Properties(); properties.UserId = user;
        var error = Assert.Throws<NativeDeliveryRejectedException>(() => NativeDelivery.Read(Subscription(), "fa.events", "income.created.v1", properties, Body()));
        Assert.Equal("Native lifecycle delivery is invalid.", error.Message); Assert.Null(error.InnerException);
    }

    [Fact]
    public void RetryIsAuthenticatedAsItsConsumerAndReturnsOnlyToItsOwnQueue()
    {
        var subscription = Subscription();
        var original = NativeDelivery.Read(subscription, "fa.events", "income.created.v1", Properties(), Body());
        var properties = Properties(subscription.BrokerUser); properties.Headers = original.RetryHeaders();
        var retry = NativeDelivery.Read(subscription, NativeSubscription.RetryReturnExchange, subscription.Queue, properties, Body());
        Assert.Equal(1, retry.Attempt); Assert.Equal("fa-income", retry.PublisherUser); Assert.True(retry.CanRetry);
        Assert.Throws<NativeDeliveryRejectedException>(() => NativeDelivery.Read(subscription, "fa.events", "income.created.v1", properties, Body()));
        properties.UserId = "fa-income";
        Assert.Throws<NativeDeliveryRejectedException>(() => NativeDelivery.Read(subscription, NativeSubscription.RetryReturnExchange, subscription.Queue, properties, Body()));
        properties.UserId = subscription.BrokerUser;
        properties.Headers[NativeDelivery.TargetHeader] = Encoding.UTF8.GetBytes("fa.financial-score.financial-events.v1");
        Assert.Throws<NativeDeliveryRejectedException>(() => NativeDelivery.Read(subscription, NativeSubscription.RetryReturnExchange, subscription.Queue, properties, Body()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(2147483647)]
    public void RetryBudgetCannotBeForgedOrExtended(int attempt)
    {
        var subscription = Subscription(); var properties = Properties(subscription.BrokerUser);
        properties.Headers = new Dictionary<string, object?>
        {
            [NativeDelivery.AttemptHeader] = attempt,
            [NativeDelivery.TargetHeader] = Encoding.UTF8.GetBytes(subscription.Queue),
            [NativeDelivery.OriginHeader] = Encoding.UTF8.GetBytes("fa-income")
        };
        Assert.Throws<NativeDeliveryRejectedException>(() => NativeDelivery.Read(subscription, NativeSubscription.RetryReturnExchange, subscription.Queue, properties, Body()));
        properties.Headers[NativeDelivery.AttemptHeader] = 3;
        var final = NativeDelivery.Read(subscription, NativeSubscription.RetryReturnExchange, subscription.Queue, properties, Body());
        Assert.False(final.CanRetry); Assert.Throws<InvalidOperationException>(() => final.RetryHeaders());
    }

    [Fact]
    public void OriginalPublisherAndCompleteRetryHeadersAreRequired()
    {
        var subscription = Subscription(); var properties = Properties(subscription.BrokerUser);
        properties.Headers = new Dictionary<string, object?> { [NativeDelivery.TargetHeader] = Encoding.UTF8.GetBytes(subscription.Queue) };
        Assert.Throws<NativeDeliveryRejectedException>(() => NativeDelivery.Read(subscription, NativeSubscription.RetryReturnExchange, subscription.Queue, properties, Body()));
        properties.Headers[NativeDelivery.AttemptHeader] = 1;
        properties.Headers[NativeDelivery.OriginHeader] = Encoding.UTF8.GetBytes("fa-profile");
        Assert.Throws<NativeDeliveryRejectedException>(() => NativeDelivery.Read(subscription, NativeSubscription.RetryReturnExchange, subscription.Queue, properties, Body()));
        properties.Headers[NativeDelivery.OriginHeader] = new byte[] { 255, 255 };
        Assert.Throws<NativeDeliveryRejectedException>(() => NativeDelivery.Read(subscription, NativeSubscription.RetryReturnExchange, subscription.Queue, properties, Body()));
    }

    [Theory]
    [InlineData(",\"value\":8")]
    [InlineData(",\"nested\":{\"value\":1,\"value\":2}")]
    [InlineData(",\"nested\":[{\"value\":1,\"value\":2}]")]
    public void DuplicateJsonPropertiesAreNeverSilentlyNormalized(string extra)
    {
        Assert.Throws<NativeDeliveryRejectedException>(() => NativeDelivery.Read(Subscription(), "fa.events", "income.created.v1", Properties(), Body(extra)));
    }

    [Fact]
    public void WrongRouteWireIdentityEncodingAndOperationalHeadersAreRejected()
    {
        var subscription = Subscription(); var properties = Properties();
        Assert.Throws<NativeDeliveryRejectedException>(() => NativeDelivery.Read(subscription, "fa.events", "expense.created.v1", properties, Body()));
        properties.MessageId = "synthetic-mismatch";
        Assert.Throws<NativeDeliveryRejectedException>(() => NativeDelivery.Read(subscription, "fa.events", "income.created.v1", properties, Body()));
        properties = Properties(); properties.CorrelationId = "private@example.invalid";
        Assert.Throws<NativeDeliveryRejectedException>(() => NativeDelivery.Read(subscription, "fa.events", "income.created.v1", properties, Body()));
        properties = Properties(); properties.ContentEncoding = "utf-16";
        Assert.Throws<NativeDeliveryRejectedException>(() => NativeDelivery.Read(subscription, "fa.events", "income.created.v1", properties, Body()));
    }

    [Fact]
    public void BindingsCannotConsumeForeignCommandsOrAmbiguousTypes()
    {
        var command = NativeBinding.Command("income", "transaction-confirmed", "transaction.confirmed.v1", "transaction-intake");
        Assert.Throws<ArgumentException>(() => new NativeSubscription("expense", "confirmed", new[] { command }));
        var subscription = new NativeSubscription("income", "confirmed", new[] { command });
        Assert.StartsWith(subscription.Queue + ".retry.5s.", subscription.RetryQueue(command, 1));
        Assert.Throws<ArgumentException>(() => subscription.RetryQueue(command, 4));
        Assert.Throws<ArgumentException>(() => subscription.RetryQueue(NativeBinding.Event("income.created.v1", "income"), 1));
        Assert.Throws<ArgumentException>(() => new NativeSubscription("analytics", "financial", new[]
            { NativeBinding.Event("income.created.v1", "income"), NativeBinding.Event("income.created.v1", "expense") }));
        Assert.Throws<ArgumentException>(() => new NativeSubscription("income", "confirmed", new[]
        {
            NativeBinding.Command("income", "transaction-confirmed", "transaction.confirmed.v1", "transaction-intake"),
            NativeBinding.Command("income", "transaction-confirmed", "transaction.confirmed.v2", "transaction-intake")
        }));
        Assert.Equal(new[] { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5) }, NativeSubscription.RetryDelays);
    }

    private sealed record Payload([property: JsonRequired] string EventId,
        [property: JsonRequired] string EventType, [property: JsonRequired] int Value);
}
