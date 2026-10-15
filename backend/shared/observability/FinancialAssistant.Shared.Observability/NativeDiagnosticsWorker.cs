using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace FinancialAssistant.Shared.Observability;

internal sealed class NativeDiagnosticsWorker(NativeDiagnosticsProvider provider) : IHostedService
{
    private Task? execution;
    public Task StartAsync(CancellationToken token) { execution = provider.RunAsync(CancellationToken.None); return Task.CompletedTask; }
    public async Task StopAsync(CancellationToken token)
    {
        provider.Complete();
        if (execution is not null) { await execution.WaitAsync(token); }
    }
}

internal sealed class NativeDiagnosticsHealthCheck(NativeDiagnosticsProvider provider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken token = default)
    {
        var counters = provider.Counters;
        return Task.FromResult(counters.DroppedRecords > 0 || counters.StorageFailures > 0
            ? HealthCheckResult.Unhealthy("Native diagnostics have a visibility gap.") : HealthCheckResult.Healthy());
    }
}
