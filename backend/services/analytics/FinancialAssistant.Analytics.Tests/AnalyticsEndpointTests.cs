using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Contracts;
using FinancialAssistant.Shared.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinancialAssistant.Analytics.Tests;

public sealed class AnalyticsEndpointTests : IClassFixture<AnalyticsWebApplicationFactory>
{
    private readonly AnalyticsWebApplicationFactory factory;
    private readonly HttpClient client;

    public AnalyticsEndpointTests(AnalyticsWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Dashboard_ReturnsOwnerScopedDeterministicAnalytics()
    {
        const string userId = "synthetic-analytics-owner";
        var referenceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        using (var scope = factory.Services.CreateScope())
        {
            var projector = scope.ServiceProvider.GetRequiredService<AnalyticsProjector>();
            await projector.ApplyAsync(
                CreateEvent(userId, referenceDate),
                CancellationToken.None);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{AnalyticsApiRoutes.GatewayDashboard}?currency=USD&timeZoneId=UTC&referenceDate={referenceDate:yyyy-MM-dd}&dailyExpenseLimit=50&trendDays=7");
        AddTrustedHeaders(request, userId);
        using var response = await client.SendAsync(request);
        var dashboard = await response.Content.ReadFromJsonAsync<AnalyticsDashboardResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dashboard);
        Assert.Equal(25m, dashboard.DailyLimit.Spent);
        Assert.Equal(25m, dashboard.DailyLimit.Remaining);
        Assert.Single(dashboard.CategoryTotals);
        Assert.Equal(7, dashboard.RecentTrend.Count);
    }

    [Fact]
    public async Task Dashboard_RequiresTrustedGatewayAndValidQuery()
    {
        using var unauthorized = await client.GetAsync(
            $"{AnalyticsApiRoutes.Dashboard}?currency=USD&timeZoneId=UTC");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var invalidRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{AnalyticsApiRoutes.Dashboard}?currency=US&timeZoneId=UTC&trendDays=32");
        AddTrustedHeaders(invalidRequest, "synthetic-invalid-owner");
        using var invalid = await client.SendAsync(invalidRequest);
        var problem = await invalid.Content.ReadFromJsonAsync<AnalyticsApiErrorResponse>();
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("invalid_analytics_request", problem?.Code);
    }

    private static IntegrationEventEnvelope<FinancialRecordChangedV1> CreateEvent(
        string userId,
        DateOnly date)
    {
        var changedAt = DateTimeOffset.UtcNow;
        return new IntegrationEventEnvelope<FinancialRecordChangedV1>(
            "synthetic-analytics-event",
            "synthetic-analytics-occurrence",
            FinancialRecordEventTypes.ExpenseCreated,
            changedAt,
            "expense-service",
            FinancialRecordEventTypes.SchemaVersion,
            "synthetic-correlation",
            "synthetic-causation",
            AnalyticsOwnerHasher.Hash(userId),
            new FinancialRecordChangedV1(
                "synthetic-expense-record",
                25m,
                "USD",
                "expense.groceries",
                date,
                "active",
                0,
                "manual",
                changedAt));
    }

    private static void AddTrustedHeaders(HttpRequestMessage request, string userId)
    {
        request.Headers.TryAddWithoutValidation(
            AnalyticsGatewayHeaders.Authentication,
            AnalyticsWebApplicationFactory.GatewaySecret);
        request.Headers.TryAddWithoutValidation(AnalyticsGatewayHeaders.UserId, userId);
    }
}
