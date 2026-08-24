using FinancialAssistant.Monitoring.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Monitoring.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMonitoringInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(MonitoringOptions.SectionName);
        services.Configure<MonitoringOptions>(section);
        var options = section.Get<MonitoringOptions>() ?? new MonitoringOptions();
        if (options.ProbeTimeoutSeconds is < 1 or > 30)
        {
            throw new InvalidOperationException("Monitoring probe timeout must be between 1 and 30 seconds.");
        }

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new MonitoringSignalPolicy(
            options.SignalPolicy.AllowedSourceServices,
            options.SignalPolicy.AllowedUiStages));
        services.AddSingleton<IMonitoringMetricStore, InMemoryMonitoringMetricStore>();
        services.AddSingleton<MonitoringSnapshotService>();
        services
            .AddHttpClient<IMonitoringDependencyProbe, HttpMonitoringDependencyProbe>(client =>
                client.Timeout = TimeSpan.FromSeconds(options.ProbeTimeoutSeconds));
        return services;
    }
}
