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
        if (options.ServiceProbeCacheSeconds is < 0 or > 5 || options.ServiceProbeCacheSeconds > 0
            && options.Services.Any(target => target.Name.Length is < 1 or > 64 || target.BaseAddress.Length > 2048))
        { throw new InvalidOperationException("Service health cache must be disabled or bounded to 1-5 seconds with bounded targets."); }
        if (options.ServiceProbeCacheSeconds > 0)
        { services.AddSingleton(provider => new MonitoringServiceProbeCache(provider.GetRequiredService<TimeProvider>(), options.ServiceProbeCacheSeconds)); }
        services.AddSingleton(new MonitoringSignalPolicy(
            options.SignalPolicy.AllowedSourceServices,
            options.SignalPolicy.AllowedUiStages));
        services.AddSingleton<IMonitoringMetricStore, InMemoryMonitoringMetricStore>();
        services.AddSingleton<MonitoringJobStore>();
        services.AddSingleton<MonitoringSnapshotService>();
        services
            .AddHttpClient<IMonitoringDependencyProbe, HttpMonitoringDependencyProbe>(client =>
                client.Timeout = TimeSpan.FromSeconds(options.ProbeTimeoutSeconds));
        return services;
    }
}
