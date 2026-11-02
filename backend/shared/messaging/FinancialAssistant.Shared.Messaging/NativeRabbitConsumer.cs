using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FinancialAssistant.Shared.Messaging;

public sealed class NativeRabbitConsumer
{
    private readonly NativeBrokerSettings settings;
    private readonly NativeSubscription subscription;
    private int running;
    private int connected;
    private long acknowledged;

    public NativeRabbitConsumer(NativeBrokerSettings settings, NativeSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(settings); ArgumentNullException.ThrowIfNull(subscription);
        if (settings.ServiceId != subscription.Owner) { throw new ArgumentException("Native consumer must use its own broker role."); }
        this.settings = settings; this.subscription = subscription;
    }

    public bool IsConnected => Volatile.Read(ref connected) != 0;
    public long AcknowledgedDeliveries => Interlocked.Read(ref acknowledged);

    public async Task RunAsync(Func<NativeDelivery, CancellationToken, Task> handler, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0) { throw new InvalidOperationException("Native consumer is already running."); }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task EndSession()
        {
            ended.TrySetResult(); lifetime.Cancel(); return Task.CompletedTask;
        }
        try
        {
            await using var connection = await settings.CreateFactory().CreateConnectionAsync(lifetime.Token);
            await using var input = await connection.CreateChannelAsync(cancellationToken: lifetime.Token);
            await using var retry = await connection.CreateChannelAsync(new CreateChannelOptions(
                publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), lifetime.Token);
            try
            {
                connection.ConnectionShutdownAsync += (_, _) => EndSession();
                input.ChannelShutdownAsync += (_, _) => EndSession();
                input.CallbackExceptionAsync += (_, _) => EndSession();
                retry.ChannelShutdownAsync += (_, _) => EndSession();
                var consumer = new AsyncEventingBasicConsumer(input);
                consumer.UnregisteredAsync += (_, _) => EndSession();
                consumer.ReceivedAsync += async (_, args) =>
                {
                    var settlement = new ChannelSettlement(settings, subscription, input, retry, args.DeliveryTag,
                        () => Interlocked.Increment(ref acknowledged));
                    try
                    {
                        NativeDelivery delivery;
                        try { delivery = NativeDelivery.Read(subscription, args.Exchange, args.RoutingKey, args.BasicProperties, args.Body); }
                        catch (NativeDeliveryRejectedException) { await settlement.RejectAsync(lifetime.Token); return; }
                        await NativeDeliveryFlow.ProcessAsync(delivery, handler, settlement, lifetime.Token);
                    }
                    catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
                    catch (Exception) { await EndSession(); }
                };
                await input.QueueDeclarePassiveAsync(subscription.Queue, lifetime.Token);
                await input.BasicQosAsync(0, 1, false, lifetime.Token);
                await input.BasicConsumeAsync(subscription.Queue, autoAck: false, consumerTag: string.Empty,
                    noLocal: false, exclusive: false, arguments: null, consumer, lifetime.Token);
                Volatile.Write(ref connected, 1);
                await ended.Task.WaitAsync(lifetime.Token);
            }
            finally { Volatile.Write(ref connected, 0); lifetime.Cancel(); }
            throw new NativeBrokerUnavailableException();
        }
        catch (Exception) when (token.IsCancellationRequested) { throw new OperationCanceledException(token); }
        catch (Exception) { throw new NativeBrokerUnavailableException(); }
        finally { Volatile.Write(ref connected, 0); Volatile.Write(ref running, 0); }
    }

    private sealed class ChannelSettlement(NativeBrokerSettings settings, NativeSubscription subscription,
        IChannel input, IChannel retry, ulong deliveryTag, Action acknowledged) : INativeDeliverySettlement
    {
        public async Task PublishRetryAsync(NativeDelivery delivery, CancellationToken token)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            var properties = new BasicProperties
            {
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                DeliveryMode = DeliveryModes.Persistent,
                MessageId = delivery.EventId,
                Type = delivery.EventType,
                UserId = settings.BrokerUser,
                Timestamp = new AmqpTimestamp(delivery.Timestamp),
                Headers = delivery.RetryHeaders()
            };
            await retry.BasicPublishAsync(NativeSubscription.RetryExchange,
                subscription.RetryQueue(delivery.Binding, delivery.Attempt + 1), mandatory: true,
                basicProperties: properties, body: delivery.Body, cancellationToken: deadline.Token);
        }

        public async Task AcknowledgeAsync(CancellationToken token)
        { await input.BasicAckAsync(deliveryTag, false, token); acknowledged(); }
        public async Task RejectAsync(CancellationToken token) => await input.BasicRejectAsync(deliveryTag, requeue: false, token);
    }
}
