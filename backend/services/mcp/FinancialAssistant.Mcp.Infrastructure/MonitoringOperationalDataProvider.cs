using System.Net.Http.Json;
using FinancialAssistant.Mcp.Application;
using FinancialAssistant.Mcp.Contracts;
using FinancialAssistant.Monitoring.Contracts;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Mcp.Infrastructure;

public sealed class MonitoringOperationalDataProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<McpOptions> options) : IMcpOperationalDataProvider
{
    public async Task<McpSystemHealthResponse> GetSystemHealthAsync(
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        return snapshot is null
            ? UnavailableHealth()
            : new McpSystemHealthResponse(
                snapshot.OverallStatus,
                snapshot.Services.Select(service => new McpServiceStatus(
                    service.Service,
                    service.Status,
                    service.LatencyMilliseconds,
                    service.ErrorCategory)).ToArray(),
                snapshot.RabbitMq.Status,
                snapshot.RabbitMq.QueueDepth,
                snapshot.Elasticsearch.Status,
                "aggregate-operational-only");
    }

    public async Task<McpAiCostSummaryResponse> GetAiCostSummaryAsync(
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        var value = snapshot?.Metrics.AiUsage;
        return new McpAiCostSummaryResponse(
            value?.RequestCount ?? 0,
            value?.SuccessfulRequestCount ?? 0,
            value?.InputTokenCount ?? 0,
            value?.OutputTokenCount ?? 0,
            value?.EstimatedCostMicros ?? 0,
            "aggregate-operational-only");
    }

    public async Task<McpParsingQualityResponse> GetParsingQualityAsync(
        CancellationToken cancellationToken)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken);
        var value = snapshot?.Metrics.ParsingQuality;
        return new McpParsingQualityResponse(
            value?.ProcessedCount ?? 0,
            value?.SuccessfulCount ?? 0,
            value?.ReviewRequiredCount ?? 0,
            value?.FailedCount ?? 0,
            value?.SuccessPercent ?? 0,
            "aggregate-operational-only");
    }

    private async Task<MonitoringDashboardResponse?> GetSnapshotAsync(
        CancellationToken cancellationToken)
    {
        var value = options.Value.Monitoring;
        if (!Uri.TryCreate(value.BaseAddress, UriKind.Absolute, out var baseAddress)
            || string.IsNullOrWhiteSpace(value.SharedSecret))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, MonitoringApiRoutes.ServiceDashboard);
            request.Headers.Add(MonitoringHeaders.GatewayAuthentication, value.SharedSecret);
            request.Headers.Add(MonitoringHeaders.GatewayRoles, McpRoles.Admin);
            var client = httpClientFactory.CreateClient(DependencyInjection.MonitoringClientName);
            client.BaseAddress = baseAddress;
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<MonitoringDashboardResponse>(
                cancellationToken: cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static McpSystemHealthResponse UnavailableHealth() =>
        new(
            "unavailable",
            [],
            "unavailable",
            0,
            "unavailable",
            "aggregate-operational-only");
}
