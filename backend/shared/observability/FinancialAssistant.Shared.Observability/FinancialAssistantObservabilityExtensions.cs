using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
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

        return builder;
    }

    public static IApplicationBuilder UseFinancialAssistantCorrelation(
        this IApplicationBuilder application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return application.UseMiddleware<FinancialAssistantCorrelationMiddleware>();
    }
}
