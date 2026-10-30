namespace FinancialAssistant.Shared.Messaging;

internal interface INativeDeliverySettlement
{
    Task PublishRetryAsync(NativeDelivery delivery, CancellationToken token);
    Task AcknowledgeAsync(CancellationToken token);
    Task RejectAsync(CancellationToken token);
}

internal static class NativeDeliveryFlow
{
    internal static async Task ProcessAsync(NativeDelivery delivery,
        Func<NativeDelivery, CancellationToken, Task> handler, INativeDeliverySettlement settlement, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            using var processing = CancellationTokenSource.CreateLinkedTokenSource(token);
            processing.CancelAfter(TimeSpan.FromSeconds(30));
            await handler(delivery, processing.Token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (NativeDeliveryRejectedException)
        {
            await settlement.RejectAsync(token);
            return;
        }
        catch (Exception)
        {
            if (!delivery.CanRetry) { await settlement.RejectAsync(token); return; }
            await settlement.PublishRetryAsync(delivery, token);
        }

        // An uncertain business commit or retry publication must remain replayable.
        // Do not turn an acknowledgement failure into another in-process publication.
        await settlement.AcknowledgeAsync(token);
    }
}
