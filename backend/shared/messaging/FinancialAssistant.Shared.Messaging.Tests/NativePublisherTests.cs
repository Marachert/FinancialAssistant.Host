using System.Net;
using System.Net.Sockets;
using System.Text;
using RabbitMQ.Client;
using Xunit;

namespace FinancialAssistant.Shared.Messaging.Tests;

public sealed class NativePublisherTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
    private static NativePublication Publication(string id = "synthetic-source") => new("fa.events", "income.created.v1",
        "income.created.v1", id, At, Encoding.UTF8.GetBytes("{}"));
    private static NativeBrokerSettings Settings(int port = 5672) => new("income",
        $"amqp://fa-income:{new string('s', 48)}@127.0.0.1:{port}/fa-test");

    [Fact]
    public void PropertiesHaveBrokerVerifiedRoleAndNoFinancialOrIdentityHeaders()
    {
        var properties = NativeRabbitPublisher.Properties(Settings(), Publication());
        Assert.Equal("fa-income", properties.UserId); Assert.Equal(DeliveryModes.Persistent, properties.DeliveryMode);
        Assert.Equal("application/json", properties.ContentType); Assert.Equal("utf-8", properties.ContentEncoding);
        Assert.Equal("income.created.v1", properties.Type); Assert.Equal("synthetic-source", properties.MessageId);
        Assert.Equal(At.ToUnixTimeSeconds(), properties.Timestamp.UnixTime);
        Assert.Null(properties.Headers); Assert.Null(properties.CorrelationId); Assert.Null(properties.ReplyTo);
    }

    [Fact]
    public async Task CallerCancellationResetsConnectionAndDoesNotLeakCredentials()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await using var publisher = new NativeRabbitPublisher(Settings(port));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publisher.PublishAsync(Publication(), cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken); Assert.False(publisher.IsConnected);
        Assert.DoesNotContain(new string('s', 48), failure.ToString());
    }

    [Fact]
    public async Task AllPublicationsMustConfirmBeforeAcknowledgingOwnedOutbox()
    {
        var acknowledged = 0; var first = Publication(); var second = Publication("synthetic-audit");
        var transport = new Publisher { FailAt = 2 };
        var work = new NativeOutboxWork(new[] { first, second }, _ => { acknowledged++; return Task.CompletedTask; });
        var dispatcher = new NativeOutboxDispatcher(transport, _ => Task.FromResult<IReadOnlyList<NativeOutboxWork>>(new[] { work }));
        await Assert.ThrowsAsync<NativeBrokerUnavailableException>(() => dispatcher.DispatchOnceAsync());
        Assert.Equal(0, acknowledged); Assert.Equal(new[] { "synthetic-source", "synthetic-audit" }, transport.Attempts);
        transport.FailAt = 0;
        Assert.Equal(1, await dispatcher.DispatchOnceAsync()); Assert.Equal(1, acknowledged);
        Assert.Equal(new[] { "synthetic-source", "synthetic-audit", "synthetic-source", "synthetic-audit" }, transport.Attempts);
    }

    [Fact]
    public async Task FailedOwnedAcknowledgementRemainsReplayableWithoutInventedDeliveryState()
    {
        var calls = 0; var transport = new Publisher();
        var work = new NativeOutboxWork(new[] { Publication() }, _ =>
        {
            if (++calls == 1) { throw new InvalidOperationException("synthetic storage failure"); }
            return Task.CompletedTask;
        });
        var dispatcher = new NativeOutboxDispatcher(transport, _ => Task.FromResult<IReadOnlyList<NativeOutboxWork>>(new[] { work }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchOnceAsync());
        Assert.Equal(1, await dispatcher.DispatchOnceAsync()); Assert.Equal(2, calls); Assert.Equal(2, transport.Attempts.Count);
    }

    [Fact]
    public async Task OversizedBatchIsRejectedBeforeAnyPublishOrAcknowledgement()
    {
        var transport = new Publisher(); var acknowledgements = 0;
        var work = new NativeOutboxWork(new[] { Publication() }, _ => { acknowledgements++; return Task.CompletedTask; });
        var dispatcher = new NativeOutboxDispatcher(transport,
            _ => Task.FromResult<IReadOnlyList<NativeOutboxWork>>(Enumerable.Repeat(work, 26).ToArray()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchOnceAsync());
        Assert.Empty(transport.Attempts); Assert.Equal(0, acknowledgements);
        Assert.Throws<ArgumentException>(() => new NativeOutboxWork([], _ => Task.CompletedTask));
        Assert.Throws<ArgumentException>(() => new NativeOutboxWork(Enumerable.Repeat(Publication(), 5).ToArray(), _ => Task.CompletedTask));
    }

    private sealed class Publisher : INativePublisher
    {
        public int FailAt { get; set; }
        public List<string> Attempts { get; } = [];
        public Task PublishAsync(NativePublication publication, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); Attempts.Add(publication.EventId);
            return Attempts.Count == FailAt ? Task.FromException(new NativeBrokerUnavailableException()) : Task.CompletedTask;
        }
    }
}
