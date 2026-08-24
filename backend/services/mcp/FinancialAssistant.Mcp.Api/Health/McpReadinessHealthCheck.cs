using FinancialAssistant.Mcp.Api.Security;
using FinancialAssistant.Mcp.Application;
using FinancialAssistant.Mcp.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Mcp.Api.Health;

public sealed class McpReadinessHealthCheck(
    IConfiguration configuration,
    IOptions<McpOptions> options,
    McpToolRegistry registry) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var secret = configuration[McpHeaderAuthenticationHandler.SharedSecretConfigurationKey];
        var ready = secret?.Length >= 32
            && registry.All.Count == 6
            && registry.All.All(tool => tool.IsReadOnly && tool.AllowedRoles.Count > 0)
            && DependencyInjection.IsReady(options.Value);
        return Task.FromResult(ready
            ? HealthCheckResult.Healthy("MCP allowlist, authentication, and audit configuration are ready.")
            : HealthCheckResult.Unhealthy("MCP configuration is incomplete."));
    }
}
