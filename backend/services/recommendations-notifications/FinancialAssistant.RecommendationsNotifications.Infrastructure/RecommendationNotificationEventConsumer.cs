using System.Text.Json;
using FinancialAssistant.Shared.Contracts.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public sealed class RecommendationNotificationEventConsumer :
    BackgroundService,
    IAsyncDisposable
{
    private const string RetryAttemptHeader = "x-fa-retry-attempt";
    private static readonly string[] RoutingKeys =
    {
        AnalyticsEventTypes.AnalyticsUpdated,
        FinancialScoreEventTypes.ScoreCalculated,
        RecommendationEventTypes.RecommendationGenerated
    };

    private readonly RecommendationNotificationMessageHandler handler;
    private readonly RecommendationNotificationEventOptions options;
    private readonly ILogger<RecommendationNotificationEventConsumer> logger;
    private IConnection? connection;
    private IChannel? channel;

    public RecommendationNotificationEventConsumer(
        RecommendationNotificationMessageHandler handler,
        IOptions<RecommendationNotificationServiceOptions> options,
        ILogger<RecommendationNotificationEventConsumer> logger)
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
            ClientProvidedName =
                "financial-assistant-recommendations-notifications:event-consumer"
        };
        connection = await factory.CreateConnectionAsync(stoppingToken);
        channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            stoppingToken);
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
            await channel!.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or FormatException)
        {
            logger.LogWarning(
                "Insight event rejected. RoutingKey={RoutingKey} FailureType={FailureType}",
                delivery.RoutingKey,
                exception.GetType().Name);
            await channel!.BasicRejectAsync(delivery.DeliveryTag, requeue: false, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (await TryScheduleRetryAsync(delivery, cancellationToken))
            {
                logger.LogWarning(
                    "Insight event scheduled for bounded retry. RoutingKey={RoutingKey} FailureType={FailureType}",
                    delivery.RoutingKey,
                    exception.GetType().Name);
                await channel!.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
                return;
            }

            logger.LogError(
                "Insight event retry budget exhausted. RoutingKey={RoutingKey} FailureType={FailureType}",
                delivery.RoutingKey,
                exception.GetType().Name);
            await channel!.BasicNackAsync(
                delivery.DeliveryTag,
                multiple: false,
                requeue: false,
                cancellationToken);
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
        await activeChannel.ExchangeDeclareAsync(
            options.RetryExchange,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await activeChannel.QueueDeclareAsync(
            options.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-exchange"] = options.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = options.Queue
            },
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

        foreach (var routingKey in RoutingKeys)
        {
            await activeChannel.QueueBindAsync(
                options.Queue,
                options.Exchange,
                routingKey,
                cancellationToken: cancellationToken);
            for (var completedAttempts = 0;
                 RecommendationNotificationRetryPolicy.TryGetNext(
                     completedAttempts,
                     out var retryStep);
                 completedAttempts++)
            {
                var retryQueue = RecommendationNotificationRetryPolicy.CreateQueueName(
                    options.Queue,
                    retryStep,
                    routingKey);
                await activeChannel.QueueDeclareAsync(
                    retryQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    new Dictionary<string, object?>
                    {
                        ["x-queue-type"] = "quorum",
                        ["x-message-ttl"] = retryStep.DelayMilliseconds,
                        ["x-dead-letter-exchange"] = options.Exchange,
                        ["x-dead-letter-routing-key"] = routingKey
                    },
                    cancellationToken: cancellationToken);
                await activeChannel.QueueBindAsync(
                    retryQueue,
                    options.RetryExchange,
                    retryQueue,
                    cancellationToken: cancellationToken);
            }
        }
    }

    private async Task<bool> TryScheduleRetryAsync(
        BasicDeliverEventArgs delivery,
        CancellationToken cancellationToken)
    {
        var completedAttempts = ReadRetryAttempts(delivery.BasicProperties.Headers);
        if (!RecommendationNotificationRetryPolicy.TryGetNext(
                completedAttempts,
                out var retryStep))
        {
            return false;
        }

        var headers = delivery.BasicProperties.Headers is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(
                delivery.BasicProperties.Headers,
                StringComparer.Ordinal);
        headers[RetryAttemptHeader] = retryStep.Attempt;
        var properties = new BasicProperties
        {
            ContentType = delivery.BasicProperties.ContentType,
            ContentEncoding = delivery.BasicProperties.ContentEncoding,
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = delivery.BasicProperties.MessageId,
            Type = delivery.BasicProperties.Type,
            CorrelationId = delivery.BasicProperties.CorrelationId,
            Timestamp = delivery.BasicProperties.Timestamp,
            Headers = headers
        };
        var retryQueue = RecommendationNotificationRetryPolicy.CreateQueueName(
            options.Queue,
            retryStep,
            delivery.RoutingKey);
        await channel!.BasicPublishAsync(
            options.RetryExchange,
            retryQueue,
            mandatory: true,
            basicProperties: properties,
            body: delivery.Body,
            cancellationToken: cancellationToken);
        return true;
    }

    private static int ReadRetryAttempts(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(RetryAttemptHeader, out var value))
        {
            return 0;
        }

        return value switch
        {
            int attempt => attempt,
            long attempt when attempt is >= 0 and <= int.MaxValue => (int)attempt,
            byte[] bytes when int.TryParse(
                System.Text.Encoding.UTF8.GetString(bytes),
                out var attempt) => attempt,
            _ => 0
        };
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString) ||
            string.IsNullOrWhiteSpace(options.Exchange) ||
            string.IsNullOrWhiteSpace(options.RetryExchange) ||
            string.IsNullOrWhiteSpace(options.DeadLetterExchange) ||
            string.IsNullOrWhiteSpace(options.Queue))
        {
            throw new InvalidOperationException(
                "RecommendationsNotifications RabbitMQ settings are required.");
        }
    }
}
