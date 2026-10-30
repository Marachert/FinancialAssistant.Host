using System.Text;
using RabbitMQ.Client;
using Xunit;

namespace FinancialAssistant.Shared.Messaging.Tests;

public sealed class NativeSourceSequenceTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("{\"eventId\":\"synthetic-event\",\"eventType\":\"score.calculated.v1\"}");
    private static NativePublication Publication(long? sequence) => new(NativePublication.EventsExchange,
        "score.calculated.v1", "score.calculated.v1", "synthetic-event", At, Body, sourceSequence: sequence);
    private static NativeSubscription Subscription() => new("recommendations-notifications", "insights",
        new[] { NativeBinding.Event("score.calculated.v1", "financial-score") });

    [Fact]
    public void OptionalOrderingDoesNotChangePublicBodyAndSurvivesOwnedRetryMetadata()
    {
        var publication = Publication(7L);
        var properties = NativeRabbitPublisher.Properties(new NativeBrokerSettings("financial-score",
            "amqp://fa-financial-score:synthetic-secret-at-least-thirty-two-chars@127.0.0.1:5672/fa-poc"), publication);
        Assert.Equal(7L, properties.Headers![NativePublication.SourceSequenceHeader]);
        var subscription = Subscription();
        var first = NativeDelivery.Read(subscription, NativePublication.EventsExchange, "score.calculated.v1", properties, Body);
        Assert.Equal(7L, first.SourceSequence);
        var retry = new BasicProperties(properties)
        { UserId = subscription.BrokerUser, Headers = first.RetryHeaders() };
        var repeated = NativeDelivery.Read(subscription, NativeSubscription.RetryReturnExchange, subscription.Queue, retry, Body);
        Assert.Equal(7L, repeated.SourceSequence); Assert.Equal("fa-financial-score", repeated.PublisherUser);
        Assert.Equal(1, repeated.Attempt); Assert.Equal(Body, publication.Body.ToArray());
        Assert.Null(NativeRabbitPublisher.Properties(new NativeBrokerSettings("financial-score",
            "amqp://fa-financial-score:synthetic-secret-at-least-thirty-two-chars@127.0.0.1:5672/fa-poc"), Publication(null)).Headers);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    public void InvalidPublicationSequenceIsRejected(long sequence) => Assert.Throws<ArgumentException>(() => Publication(sequence));

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    [InlineData(7)]
    [InlineData("7")]
    public void NonCanonicalInboundSequenceCannotEnterNativeProcessing(object value)
    {
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = "synthetic-event",
            Type = "score.calculated.v1",
            UserId = "fa-financial-score",
            Timestamp = new AmqpTimestamp(At.ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?> { [NativePublication.SourceSequenceHeader] = value }
        };
        Assert.Throws<NativeDeliveryRejectedException>(() => NativeDelivery.Read(Subscription(), NativePublication.EventsExchange,
            "score.calculated.v1", properties, Body));
    }
}
