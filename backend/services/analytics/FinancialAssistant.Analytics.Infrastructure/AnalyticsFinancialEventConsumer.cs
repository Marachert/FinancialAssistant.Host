using System.Text.Json;
using FinancialAssistant.Shared.Contracts.Events;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FinancialAssistant.Analytics.Infrastructure;

public sealed class AnalyticsFinancialEventConsumer : BackgroundService, IAsyncDisposable
{
    private static readonly string[] RoutingKeys =
    {
        FinancialRecordEventTypes.IncomeCreated,
        FinancialRecordEventTypes.IncomeUpdated,
        FinancialRecordEventTypes.IncomeArchived,
        FinancialRecordEventTypes.IncomeRestored,
        FinancialRecordEventTypes.ExpenseCreated,
        FinancialRecordEventTypes.ExpenseUpdated,
        FinancialRecordEventTypes.ExpenseArchived,
        FinancialRecordEventTypes.ExpenseRestored
    };

    private readonly AnalyticsFinancialEventMessageHandler handler;
    private readonly AnalyticsEventConsumerOptions options;
    private readonly ILogger<AnalyticsFinancialEventConsumer> logger;
    private IConnection? connection;
    private IChannel? channel;

    public AnalyticsFinancialEventConsumer(
        AnalyticsFinancialEventMessageHandler handler,
        IOptions<AnalyticsServiceOptions> options,
        ILogger<AnalyticsFinancialEventConsumer> logger)
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
            ClientProvidedName = "financial-assistant-analytics-service:event-consumer"
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
            await channel!.BasicAckAsync(
                delivery.DeliveryTag,
                multiple: false,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException or FormatException)
        {
            logger.LogWarning(
                "Analytics event rejected. RoutingKey={RoutingKey} FailureType={FailureType}",
                delivery.RoutingKey,
                exception.GetType().Name);
            await channel!.BasicRejectAsync(
                delivery.DeliveryTag,
                requeue: false,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                "Analytics event processing failed. RoutingKey={RoutingKey} FailureType={FailureType}",
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

        var applicationArguments = new Dictionary<string, object?>
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
            applicationArguments,
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
        }
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString) ||
            string.IsNullOrWhiteSpace(options.Exchange) ||
            string.IsNullOrWhiteSpace(options.DeadLetterExchange) ||
            string.IsNullOrWhiteSpace(options.Queue))
        {
            throw new InvalidOperationException(
                "Analytics RabbitMQ connection, exchange, dead-letter exchange, and queue are required.");
        }
    }
}
