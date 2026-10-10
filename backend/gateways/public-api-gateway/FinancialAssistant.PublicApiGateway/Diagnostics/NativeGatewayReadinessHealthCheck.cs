using System.Net;
using FinancialAssistant.PublicApiGateway.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FinancialAssistant.PublicApiGateway.Diagnostics;

public sealed class NativeGatewayReadinessHealthCheck(
    IHttpClientFactory clients,
    GatewayDestinationCatalog destinations) : IHealthCheck
{
    public const string ClientName = "native-gateway-readiness";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var client = clients.CreateClient(ClientName);
        var addresses = destinations.Destinations
            .Where(destination => destination.Enabled)
            .Select(destination => destination.BaseAddress)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var results = await Task.WhenAll(addresses.Select(async address =>
        {
            try
            {
                using var response = await client.GetAsync(
                    new Uri(new Uri(address), "/health/ready"),
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                return response.StatusCode == HttpStatusCode.OK;
            }
            catch (HttpRequestException) { return false; }
            catch (OperationCanceledException) { return false; }
        }));
        return results.Length != 0 && results.All(result => result)
            ? HealthCheckResult.Healthy("Native downstream process checks passed; this is not installed POC acceptance.")
            : HealthCheckResult.Unhealthy("A required native downstream process is unavailable.");
    }
}
