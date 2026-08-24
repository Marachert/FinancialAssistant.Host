using FinancialAssistant.Monitoring.Infrastructure;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Monitoring.Api.Health;

public sealed class MonitoringReadinessHealthCheck(IOptions<MonitoringOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var value = options.Value;
        var validServices = value.Services.Length > 0
            && value.Services.All(service =>
                IsSafeName(service.Name)
                && Uri.TryCreate(service.BaseAddress, UriKind.Absolute, out _))
            && value.Services.Select(service => service.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() == value.Services.Length;
        var validDependencies = Uri.TryCreate(
                value.RabbitMq.ManagementBaseAddress,
                UriKind.Absolute,
                out _)
            && Uri.TryCreate(value.Elasticsearch.BaseAddress, UriKind.Absolute, out _);
        return Task.FromResult(validServices && validDependencies
            ? HealthCheckResult.Healthy("Monitoring probe configuration is ready.")
            : HealthCheckResult.Unhealthy("Monitoring probe configuration is incomplete."));
    }

    private static bool IsSafeName(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 64
        && value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
