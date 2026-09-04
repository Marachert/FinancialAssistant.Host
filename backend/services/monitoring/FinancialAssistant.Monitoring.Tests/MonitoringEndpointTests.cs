using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinancialAssistant.Monitoring.Application;
using FinancialAssistant.Monitoring.Contracts;
using Xunit;

namespace FinancialAssistant.Monitoring.Tests;

public sealed class MonitoringEndpointTests : IClassFixture<MonitoringWebApplicationFactory>
{
    private readonly HttpClient client;

    public MonitoringEndpointTests(MonitoringWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoints_ReportLiveAndReady()
    {
        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready");
        var livePayload = await live.Content.ReadFromJsonAsync<JsonElement>();
        var readyPayload = await ready.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal("healthy", livePayload.GetProperty("status").GetString());
        Assert.Equal("healthy", readyPayload.GetProperty("status").GetString());
        Assert.Single(livePayload.GetProperty("checks").EnumerateArray());
        Assert.Equal(2, readyPayload.GetProperty("checks").GetArrayLength());
    }

    [Fact]
    public async Task Dashboard_RequiresTrustedGatewayAndAdminRole()
    {
        using var unauthorized = await client.GetAsync(MonitoringApiRoutes.Dashboard);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var nonAdminRequest = CreateAdminRequest(MonitoringApiRoutes.Dashboard, "user");
        using var nonAdmin = await client.SendAsync(nonAdminRequest);
        Assert.Equal(HttpStatusCode.Forbidden, nonAdmin.StatusCode);

        using var adminRequest = CreateAdminRequest(MonitoringApiRoutes.Dashboard, "user,admin");
        using var admin = await client.SendAsync(adminRequest);
        var dashboard = await admin.Content.ReadFromJsonAsync<MonitoringDashboardResponse>();

        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        Assert.NotNull(dashboard);
        Assert.Equal(MonitoringStatuses.Degraded, dashboard.OverallStatus);
        Assert.Equal("aggregate-operational-only", dashboard.DataClassification);
        Assert.Equal(2, dashboard.Services.Count);
        Assert.Equal(4, dashboard.Readiness.ComponentCount);
        Assert.Equal(3, dashboard.Readiness.HealthyCount);
        Assert.Equal(1, dashboard.Readiness.DegradedCount);
        Assert.Equal(4, dashboard.RabbitMq.QueueDepth);
        Assert.Equal("green", dashboard.Elasticsearch.ClusterStatus);
    }

    [Theory]
    [InlineData(MonitoringStatuses.Healthy, MonitoringStatuses.Healthy)]
    [InlineData(MonitoringStatuses.Degraded, MonitoringStatuses.Degraded)]
    [InlineData(MonitoringStatuses.NotConfigured, MonitoringStatuses.Degraded)]
    [InlineData(MonitoringStatuses.Unavailable, MonitoringStatuses.Unavailable)]
    [InlineData("unknown", MonitoringStatuses.Unavailable)]
    public void DashboardStatusPolicy_UsesExplicitWorstStateRules(
        string componentStatus,
        string expectedOverallStatus)
    {
        var summary = MonitoringStatusPolicy.Summarize([MonitoringStatuses.Healthy, componentStatus]);

        Assert.Equal(expectedOverallStatus, MonitoringStatusPolicy.GetOverallStatus(summary));
    }

    [Fact]
    public async Task Signals_RequireTrustedServiceAndAggregateOnlyAllowlistedNumbers()
    {
        using var unauthorized = await client.PostAsJsonAsync(
            MonitoringApiRoutes.AiUsageSignals,
            new MonitoringAiUsageSignalRequest("ai-orchestration", 2, 2, 100, 25, 40));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var invalidRequest = CreateSignalRequest(
            MonitoringApiRoutes.ParsingQualitySignals,
            new MonitoringParsingQualitySignalRequest("unknown-service", 1, 1, 0, 0));
        using var invalid = await client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        using var aiRequest = CreateSignalRequest(
            MonitoringApiRoutes.AiUsageSignals,
            new MonitoringAiUsageSignalRequest("ai-orchestration", 2, 1, 100, 25, 40));
        using var ai = await client.SendAsync(aiRequest);
        using var parsingRequest = CreateSignalRequest(
            MonitoringApiRoutes.ParsingQualitySignals,
            new MonitoringParsingQualitySignalRequest("receipt-processing", 4, 3, 1, 0));
        using var parsing = await client.SendAsync(parsingRequest);
        using var funnelRequest = CreateSignalRequest(
            MonitoringApiRoutes.UiFunnelSignals,
            new MonitoringUiFunnelSignalRequest("mobile-client", "draft-confirmation", 5, 4));
        using var funnel = await client.SendAsync(funnelRequest);

        Assert.Equal(HttpStatusCode.Accepted, ai.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, parsing.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, funnel.StatusCode);

        using var dashboardRequest = CreateAdminRequest(MonitoringApiRoutes.Dashboard, "admin");
        using var response = await client.SendAsync(dashboardRequest);
        var dashboard = await response.Content.ReadFromJsonAsync<MonitoringDashboardResponse>();

        Assert.NotNull(dashboard);
        Assert.Equal(2, dashboard.Metrics.AiUsage.RequestCount);
        Assert.Equal(40, dashboard.Metrics.AiUsage.EstimatedCostMicros);
        Assert.Equal(4, dashboard.Metrics.ParsingQuality.ProcessedCount);
        Assert.Equal(75m, dashboard.Metrics.ParsingQuality.SuccessPercent);
        var stage = Assert.Single(dashboard.Metrics.UiFunnel);
        Assert.Equal("draft-confirmation", stage.Stage);
        Assert.Equal(80m, stage.CompletionPercent);
    }

    [Fact]
    public void PublicContracts_ContainNoRawUserOrFinancialPayloadFields()
    {
        var contract = File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                "backend/services/monitoring/FinancialAssistant.Monitoring.Contracts/MonitoringContracts.cs"));

        foreach (var prohibited in new[]
                 {
                     "UserId",
                     "OwnerHash",
                     "Email",
                     "Phone",
                     "Amount",
                     "ReceiptText",
                     "OcrText",
                     "Prompt",
                     "ResponseText",
                     "Note"
                 })
        {
            Assert.DoesNotContain(prohibited, contract, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static HttpRequestMessage CreateAdminRequest(string route, string roles)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, route);
        request.Headers.TryAddWithoutValidation(
            MonitoringHeaders.GatewayAuthentication,
            MonitoringWebApplicationFactory.GatewaySecret);
        request.Headers.TryAddWithoutValidation(MonitoringHeaders.GatewayRoles, roles);
        return request;
    }

    private static HttpRequestMessage CreateSignalRequest<T>(string route, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.TryAddWithoutValidation(
            MonitoringHeaders.SignalAuthentication,
            MonitoringWebApplicationFactory.SignalSecret);
        return request;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate FinancialAssistant.Backend.sln.");
    }
}
