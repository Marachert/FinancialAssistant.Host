using FinancialAssistant.Shared.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FinancialAssistant.Shared.Observability;

public static class FinancialAssistantObservabilityExtensions
{
    public static WebApplicationBuilder AddFinancialAssistantObservability(
        this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            options.UseUtcTimestamp = true;
        });

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationPropagationHandler>();
        builder.Services.ConfigureHttpClientDefaults(
            client => client.AddHttpMessageHandler<CorrelationPropagationHandler>());
        builder.Services.AddSingleton(
            new ObservabilityRuntimeIdentity(
                builder.Environment.ApplicationName,
                builder.Environment.EnvironmentName));

        if (builder.Environment.IsEnvironment(FinancialAssistantHost.NativeEnvironment))
        {
            var options = builder.Configuration.GetSection("NativeDiagnostics").Get<NativeDiagnosticsOptions>() ?? new NativeDiagnosticsOptions();
            if (options.Enabled)
            {
                options.Validate();
                builder.Services.AddSingleton(provider => new NativeDiagnosticsProvider(options,
                    provider.GetRequiredService<NativeServiceDefinition>().Id, TimeProvider.System));
                builder.Services.AddSingleton<ILoggerProvider>(provider => provider.GetRequiredService<NativeDiagnosticsProvider>());
                builder.Services.AddHostedService<NativeDiagnosticsWorker>();
                builder.Services.AddHealthChecks().AddCheck<NativeDiagnosticsHealthCheck>("native-diagnostics", tags: new[] { "ready" });
            }
            else
            {
                builder.Services.AddHealthChecks().AddCheck("native-diagnostics", () =>
                    Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("Native diagnostics are not configured; collector acceptance remains pending."), tags: new[] { "ready" });
            }
        }

        return builder;
    }

    public static IApplicationBuilder UseFinancialAssistantCorrelation(
        this IApplicationBuilder application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return application.UseMiddleware<FinancialAssistantCorrelationMiddleware>();
    }
}
