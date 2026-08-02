using System.Collections.Concurrent;
using System.Text.Json;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Shared.Contracts.Events;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FinancialAssistant.Analytics.Infrastructure;

public sealed class InMemoryAnalyticsEventPublisher : IAnalyticsEventPublisher
{
    private readonly ConcurrentQueue<IntegrationEventEnvelope<AnalyticsUpdatedV1>> published = new();

    public IReadOnlyCollection<IntegrationEventEnvelope<AnalyticsUpdatedV1>> Published =>
        published.ToArray();

    public Task PublishAsync(
        IntegrationEventEnvelope<AnalyticsUpdatedV1> envelope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        published.Enqueue(envelope);
        return Task.CompletedTask;
    }
}

public sealed class RabbitMqAnalyticsEventPublisher :
    IAnalyticsEventPublisher,
    IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AnalyticsEventConsumerOptions options;
    private readonly SemaphoreSlim publishGate = new(1, 1);
    private IConnection? connection;
    private IChannel? channel;

    public RabbitMqAnalyticsEventPublisher(IOptions<AnalyticsServiceOptions> options)
    {
        this.options = options.Value.Events;
    }

    public async Task PublishAsync(
        IntegrationEventEnvelope<AnalyticsUpdatedV1> envelope,
        CancellationToken cancellationToken)
    {
        await publishGate.WaitAsync(cancellationToken);
        try
        {
            var activeChannel = await GetChannelAsync(cancellationToken);
            await activeChannel.BasicPublishAsync(
                options.Exchange,
                envelope.EventType,
                mandatory: true,
                basicProperties: new BasicProperties
                {
                    ContentType = "application/json",
                    ContentEncoding = "utf-8",
                    DeliveryMode = DeliveryModes.Persistent,
                    MessageId = envelope.EventId,
                    Type = envelope.EventType,
                    CorrelationId = envelope.CorrelationId,
                    Timestamp = new AmqpTimestamp(envelope.OccurredAtUtc.ToUnixTimeSeconds())
                },
                body: JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions),
                cancellationToken: cancellationToken);
        }
        finally
        {
            publishGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (channel is not null)
        {
            await channel.DisposeAsync();
        }

        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        publishGate.Dispose();
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (channel?.IsOpen == true)
        {
            return channel;
        }

        if (string.IsNullOrWhiteSpace(options.ConnectionString) ||
            string.IsNullOrWhiteSpace(options.Exchange))
        {
            throw new InvalidOperationException(
                "Analytics RabbitMQ connection and exchange are required.");
        }

        var factory = new ConnectionFactory
        {
            Uri = new Uri(options.ConnectionString),
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ClientProvidedName = "financial-assistant-analytics-service:event-publisher"
        };
        connection = await factory.CreateConnectionAsync(cancellationToken);
        channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await channel.ExchangeDeclareAsync(
            options.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        return channel;
    }
}
