using FinancialAssistant.Monitoring.Application;
using FinancialAssistant.Monitoring.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialAssistant.Monitoring.Tests;

public sealed class MonitoringWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string GatewaySecret = "synthetic-monitoring-gateway-secret-2026";
    public const string SignalSecret = "synthetic-monitoring-signal-secret-2026";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Monitoring:Gateway:SharedSecret"] = GatewaySecret,
                    ["Monitoring:Signals:SharedSecret"] = SignalSecret
                }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMonitoringDependencyProbe>();
            services.AddSingleton<IMonitoringDependencyProbe, SyntheticMonitoringDependencyProbe>();
        });
    }
}

public sealed class SyntheticMonitoringDependencyProbe : IMonitoringDependencyProbe
{
    public Task<MonitoringProbeSnapshot> ProbeAsync(CancellationToken cancellationToken)
    {
        var checkedAt = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        return Task.FromResult(new MonitoringProbeSnapshot(
            [
                new MonitoringServiceProbe(
                    "analytics",
                    MonitoringStatuses.Healthy,
                    12,
                    checkedAt,
                    null),
                new MonitoringServiceProbe(
                    "receipt-processing",
                    MonitoringStatuses.Degraded,
                    25,
                    checkedAt,
                    "http_status")
            ],
            new MonitoringRabbitMqProbe(
                MonitoringStatuses.Healthy,
                7,
                4,
                2,
                checkedAt,
                null),
            new MonitoringElasticsearchProbe(
                MonitoringStatuses.Healthy,
                9,
                "green",
                1,
                12,
                checkedAt,
                null)));
    }
}
