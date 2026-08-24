using FinancialAssistant.Mcp.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Mcp.Infrastructure;

public static class DependencyInjection
{
    public const string MonitoringClientName = "mcp-monitoring";
    public const string AuditClientName = "mcp-audit";

    public static IServiceCollection AddMcpInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(McpOptions.SectionName);
        var options = section.Get<McpOptions>() ?? new McpOptions();
        Validate(options);
        services.Configure<McpOptions>(section);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<McpToolRegistry>();
        services.AddSingleton<McpToolExecutor>();
        services.AddSingleton<IMcpOperationalDataProvider, MonitoringOperationalDataProvider>();
        services.AddSingleton<IMcpReferenceDataProvider, McpReferenceDataProvider>();
        services.AddHttpClient(MonitoringClientName, client =>
            client.Timeout = TimeSpan.FromSeconds(options.Monitoring.TimeoutSeconds));
        services.AddHttpClient(AuditClientName, client =>
            client.Timeout = TimeSpan.FromSeconds(options.Audit.TimeoutSeconds));

        if (string.Equals(options.Audit.Mode, "Http", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IMcpAuditSink, HttpMcpAuditSink>();
        }
        else
        {
            services.AddSingleton<InMemoryMcpAuditSink>();
            services.AddSingleton<IMcpAuditSink>(provider =>
                provider.GetRequiredService<InMemoryMcpAuditSink>());
        }

        return services;
    }

    public static bool IsReady(McpOptions options) =>
        IsTimeoutValid(options.Monitoring.TimeoutSeconds)
        && IsTimeoutValid(options.Audit.TimeoutSeconds)
        && IsPromptEvaluationValid(options.PromptEvaluations)
        && (string.Equals(options.Audit.Mode, "InMemory", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(options.Audit.Mode, "Http", StringComparison.OrdinalIgnoreCase)
                && Uri.TryCreate(options.Audit.BaseAddress, UriKind.Absolute, out _)
                && options.Audit.SharedSecret.Length >= 32));

    private static void Validate(McpOptions options)
    {
        if (!IsReady(options))
        {
            throw new InvalidOperationException("MCP operational, audit, or prompt evaluation configuration is invalid.");
        }

        if (!string.IsNullOrWhiteSpace(options.Monitoring.BaseAddress)
            && (!Uri.TryCreate(options.Monitoring.BaseAddress, UriKind.Absolute, out _)
                || options.Monitoring.SharedSecret.Length < 32))
        {
            throw new InvalidOperationException(
                "Configured MCP Monitoring adapter requires an absolute URL and 32-character secret.");
        }
    }

    private static bool IsTimeoutValid(int value) => value is >= 1 and <= 30;

    private static bool IsPromptEvaluationValid(McpOptions.PromptEvaluationOptions value) =>
        value.EvaluatedCount >= 0
        && value.PassedCount >= 0
        && value.FailedCount >= 0
        && value.PassedCount + value.FailedCount <= value.EvaluatedCount
        && !string.IsNullOrWhiteSpace(value.Status)
        && !string.IsNullOrWhiteSpace(value.EvaluationSetVersion);
}
