using System.Text.Json;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Messaging;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FinancialAssistant.Identity.Infrastructure.Messaging;

public sealed class RabbitMqIdentityEventTransport : IIdentityEventTransport, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IdentityServiceOptions options;
    private readonly SemaphoreSlim publishGate = new(1, 1);
    private IConnection? connection;
    private IChannel? channel;

    public RabbitMqIdentityEventTransport(IOptions<IdentityServiceOptions> options)
    {
        this.options = options.Value;
    }

    public async Task PublishAsync(
        IdentityIntegrationEventEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        await publishGate.WaitAsync(cancellationToken);
        try
        {
            var activeChannel = await GetChannelAsync(cancellationToken);
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                DeliveryMode = 2,
                MessageId = envelope.EventId,
                Type = envelope.EventType,
                CorrelationId = envelope.CorrelationId,
                Timestamp = new AmqpTimestamp(envelope.OccurredAt.ToUnixTimeSeconds())
            };
            var body = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);

            await activeChannel.BasicPublishAsync(
                options.Events.Exchange,
                envelope.EventType,
                mandatory: true,
                basicProperties: properties,
                body: body,
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

        if (string.IsNullOrWhiteSpace(options.Events.ConnectionString))
        {
            throw new InvalidOperationException(
                "Identity:Events:ConnectionString is required when RabbitMq mode is enabled.");
        }

        var factory = new ConnectionFactory
        {
            Uri = new Uri(options.Events.ConnectionString),
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ClientProvidedName = $"{options.ServiceName}:event-publisher"
        };
        connection = await factory.CreateConnectionAsync(cancellationToken);
        channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        await channel.ExchangeDeclareAsync(
            options.Events.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        return channel;
    }
}
