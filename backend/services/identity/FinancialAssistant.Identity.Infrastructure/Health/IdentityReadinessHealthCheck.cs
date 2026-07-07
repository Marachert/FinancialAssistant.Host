using FinancialAssistant.Identity.Infrastructure.Configuration;
using FinancialAssistant.Identity.Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Identity.Infrastructure.Health;

public sealed class IdentityReadinessHealthCheck : IHealthCheck
{
    private readonly IOptions<IdentityServiceOptions> options;

    public IdentityReadinessHealthCheck(IOptions<IdentityServiceOptions> options)
    {
        this.options = options;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(configuration.ServiceName))
        {
            failures.Add("Identity service name is missing.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Storage.Provider))
        {
            failures.Add("Identity storage provider is missing.");
        }

        try
        {
            _ = IdentityIndexCatalog.Create(
                configuration.Storage.Environment,
                configuration.Storage.SchemaVersion,
                configuration.Storage.InitialGeneration);
        }
        catch (ArgumentException exception)
        {
            failures.Add($"Identity storage naming configuration is invalid: {exception.Message}");
        }

        var cleanup = configuration.Storage.Cleanup;
        if (cleanup.DeletedAccountRetentionDays < 1
            || cleanup.RemovedCredentialRetentionDays < 1
            || cleanup.TerminalSessionRetentionDays < 1
            || cleanup.HardMaximumSessionDocumentDays < cleanup.TerminalSessionRetentionDays
            || cleanup.RemovedProviderLinkRetentionDays < 1)
        {
            failures.Add("Identity cleanup retention configuration is invalid.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Events.Mode))
        {
            failures.Add("Identity event publishing mode is missing.");
        }

        if (string.Equals(configuration.Events.Mode, "active", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(configuration.Events.Exchange))
        {
            failures.Add("Identity event exchange is required in active mode.");
        }

        var result = failures.Count == 0
            ? HealthCheckResult.Healthy("Identity service storage and event configuration is ready.")
            : HealthCheckResult.Unhealthy(string.Join(" ", failures));

        return Task.FromResult(result);
    }
}
