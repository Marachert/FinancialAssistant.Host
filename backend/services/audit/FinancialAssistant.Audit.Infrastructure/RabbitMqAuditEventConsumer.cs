using System.Text.Json;
using FinancialAssistant.Audit.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FinancialAssistant.Audit.Infrastructure;

public sealed class RabbitMqAuditEventConsumer : BackgroundService, IAsyncDisposable
{
    private readonly AuditEventMessageHandler handler;
    private readonly AuditEventConsumerOptions options;
    private readonly ILogger<RabbitMqAuditEventConsumer> logger;
    private IConnection? connection;
    private IChannel? channel;

    public RabbitMqAuditEventConsumer(
        AuditEventMessageHandler handler,
        IOptions<AuditOptions> options,
        ILogger<RabbitMqAuditEventConsumer> logger)
    {
        this.handler = handler;
        this.options = options.Value.Events;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!string.Equals(options.Mode, "RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ValidateOptions();
        var factory = new ConnectionFactory
        {
            Uri = new Uri(options.ConnectionString),
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ClientProvidedName = "financial-assistant-audit-service:event-consumer"
        };
        connection = await factory.CreateConnectionAsync(stoppingToken);
        channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await DeclareTopologyAsync(channel, stoppingToken);
        await channel.BasicQosAsync(0, 16, global: false, cancellationToken: stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleDeliveryAsync(delivery, stoppingToken);
        await channel.BasicConsumeAsync(
            options.Queue,
            autoAck: false,
            consumer,
            cancellationToken: stoppingToken);
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
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

        base.Dispose();
    }

    private async Task HandleDeliveryAsync(
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        try
        {
            await handler.HandleAsync(delivery.Body, cancellationToken);
            await channel!.BasicAckAsync(delivery.DeliveryTag, false, cancellationToken);
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or InvalidOperationException)
        {
            logger.LogWarning(
                "Audit event rejected. RoutingKey={RoutingKey} FailureType={FailureType}",
                delivery.RoutingKey,
                exception.GetType().Name);
            await channel!.BasicRejectAsync(delivery.DeliveryTag, false, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                "Audit event processing failed. RoutingKey={RoutingKey} FailureType={FailureType}",
                delivery.RoutingKey,
                exception.GetType().Name);
            await channel!.BasicNackAsync(delivery.DeliveryTag, false, false, cancellationToken);
        }
    }

    private async Task DeclareTopologyAsync(
        IChannel activeChannel,
        CancellationToken cancellationToken)
    {
        await activeChannel.ExchangeDeclareAsync(
            options.Exchange,
            ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        await activeChannel.ExchangeDeclareAsync(
            options.DeadLetterExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);
        var arguments = new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-dead-letter-exchange"] = options.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = options.Queue
        };
        await activeChannel.QueueDeclareAsync(
            options.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments,
            cancellationToken: cancellationToken);
        var deadLetterQueue = $"{options.Queue}.dead-letter";
        await activeChannel.QueueDeclareAsync(
            deadLetterQueue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            new Dictionary<string, object?> { ["x-queue-type"] = "quorum" },
            cancellationToken: cancellationToken);
        await activeChannel.QueueBindAsync(
            deadLetterQueue,
            options.DeadLetterExchange,
            options.Queue,
            cancellationToken: cancellationToken);
        await activeChannel.QueueBindAsync(
            options.Queue,
            options.Exchange,
            options.RoutingKey,
            cancellationToken: cancellationToken);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString)
            || string.IsNullOrWhiteSpace(options.Exchange)
            || string.IsNullOrWhiteSpace(options.DeadLetterExchange)
            || string.IsNullOrWhiteSpace(options.Queue)
            || !string.Equals(options.RoutingKey, AuditEventTypes.Recorded, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Audit RabbitMQ connection and canonical topology settings are required.");
        }
    }
}
