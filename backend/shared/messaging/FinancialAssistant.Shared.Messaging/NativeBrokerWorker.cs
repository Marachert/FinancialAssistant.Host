using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinancialAssistant.Shared.Messaging;

public sealed class NativeBrokerWorker : BackgroundService
{
    private readonly Func<CancellationToken, Task> operation;
    private readonly TimeProvider clock;
    private readonly ILogger<NativeBrokerWorker> logger;

    public NativeBrokerWorker(Func<CancellationToken, Task> operation, TimeProvider clock, ILogger<NativeBrokerWorker> logger)
    {
        ArgumentNullException.ThrowIfNull(operation); ArgumentNullException.ThrowIfNull(clock); ArgumentNullException.ThrowIfNull(logger);
        this.operation = operation; this.clock = clock; this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await operation(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception)
            { logger.LogWarning("Native lifecycle attempt failed; retrying with bounded backoff."); }
            try { await Task.Delay(TimeSpan.FromSeconds(2), clock, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
