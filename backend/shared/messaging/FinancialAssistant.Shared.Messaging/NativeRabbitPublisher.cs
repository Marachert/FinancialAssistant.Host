using RabbitMQ.Client;

namespace FinancialAssistant.Shared.Messaging;

public sealed class NativeRabbitPublisher(NativeBrokerSettings settings) : INativePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private IConnection? connection;
    private IChannel? channel;
    private int disposed;

    public bool IsConnected => Volatile.Read(ref disposed) == 0 && connection?.IsOpen == true && channel?.IsOpen == true;

    public async Task PublishAsync(NativePublication publication, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publication);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        var entered = false;
        try
        {
            await gate.WaitAsync(deadline.Token); entered = true;
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            var active = await GetChannelAsync(deadline.Token);
            await active.BasicPublishAsync(publication.Exchange, publication.RoutingKey,
                mandatory: true, basicProperties: Properties(settings, publication), body: publication.Body,
                cancellationToken: deadline.Token);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            if (entered) { await ResetAsync(); }
            throw new OperationCanceledException(cancellationToken);
        }
        catch (Exception)
        {
            if (entered) { await ResetAsync(); }
            throw new NativeBrokerUnavailableException();
        }
        finally { if (entered) { gate.Release(); } }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken token)
    {
        if (channel?.IsOpen == true && connection?.IsOpen == true) { return channel; }
        await ResetAsync();
        connection = await settings.CreateFactory().CreateConnectionAsync(token);
        channel = await connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), token);
        return channel;
    }

    internal static BasicProperties Properties(NativeBrokerSettings settings, NativePublication publication) => new()
    {
        ContentType = "application/json",
        ContentEncoding = "utf-8",
        DeliveryMode = DeliveryModes.Persistent,
        MessageId = publication.EventId,
        Type = publication.EventType,
        UserId = settings.BrokerUser,
        Timestamp = new AmqpTimestamp(publication.OccurredAtUtc.ToUnixTimeSeconds()),
        Headers = publication.SourceSequence is { } sequence ? new Dictionary<string, object?>
        { [NativePublication.SourceSequenceHeader] = sequence } : null
    };

    private async Task ResetAsync()
    {
        var oldChannel = channel; var oldConnection = connection;
        channel = null; connection = null;
        if (oldChannel is not null) { try { await oldChannel.DisposeAsync(); } catch (Exception) { } }
        if (oldConnection is not null) { try { await oldConnection.DisposeAsync(); } catch (Exception) { } }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) { return; }
        await gate.WaitAsync();
        try { await ResetAsync(); }
        finally { gate.Release(); }
    }
}
