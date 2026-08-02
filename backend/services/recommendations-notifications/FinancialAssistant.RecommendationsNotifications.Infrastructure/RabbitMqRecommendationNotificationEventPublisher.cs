using System.Text.Json;
using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.Shared.Contracts.Events;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public sealed class RabbitMqRecommendationNotificationEventPublisher :
    IRecommendationEventPublisher,
    INotificationEventPublisher,
    IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RecommendationNotificationEventOptions options;
    private readonly SemaphoreSlim publishGate = new(1, 1);
    private IConnection? connection;
    private IChannel? channel;

    public RabbitMqRecommendationNotificationEventPublisher(
        IOptions<RecommendationNotificationServiceOptions> options)
    {
        this.options = options.Value.Events;
    }

    public Task PublishAsync(
        IntegrationEventEnvelope<RecommendationGeneratedV1> envelope,
        CancellationToken cancellationToken) =>
        PublishCoreAsync(envelope, cancellationToken);

    public Task PublishAsync(
        IntegrationEventEnvelope<NotificationPreparedV1> envelope,
        CancellationToken cancellationToken) =>
        PublishCoreAsync(envelope, cancellationToken);

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

    private async Task PublishCoreAsync<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken)
    {
        await publishGate.WaitAsync(cancellationToken);
        try
        {
            var activeChannel = await GetChannelAsync(cancellationToken);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = envelope.EventId,
                Type = envelope.EventType,
                CorrelationId = envelope.CorrelationId,
                Timestamp = new AmqpTimestamp(envelope.OccurredAtUtc.ToUnixTimeSeconds())
            };
            await activeChannel.BasicPublishAsync(
                options.Exchange,
                envelope.EventType,
                mandatory: true,
                basicProperties: properties,
                body: JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions),
                cancellationToken: cancellationToken);
        }
        finally
        {
            publishGate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (channel?.IsOpen == true)
        {
            return channel;
        }

        ValidateOptions();
        var factory = new ConnectionFactory
        {
            Uri = new Uri(options.ConnectionString),
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ClientProvidedName =
                "financial-assistant-recommendations-notifications:event-publisher"
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

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString) ||
            string.IsNullOrWhiteSpace(options.Exchange))
        {
            throw new InvalidOperationException(
                "RecommendationsNotifications RabbitMQ connection and exchange are required.");
        }
    }
}
