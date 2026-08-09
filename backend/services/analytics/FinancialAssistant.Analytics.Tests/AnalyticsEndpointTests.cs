using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Contracts;
using FinancialAssistant.Analytics.Infrastructure;
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
            scope.ServiceProvider
                .GetRequiredService<InMemoryAnalyticsDailyLimitProvider>()
                .Set(
                    AnalyticsOwnerHasher.Hash(userId),
                    "USD",
                    50m,
                    100m,
                    300m);
            await projector.ApplyAsync(
                CreateEvent(userId, referenceDate),
                CancellationToken.None);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{AnalyticsApiRoutes.GatewayDashboard}?currency=USD&timeZoneId=UTC&referenceDate={referenceDate:yyyy-MM-dd}&trendDays=7");
        AddTrustedHeaders(request, userId);
        using var response = await client.SendAsync(request);
        var dashboard = await response.Content.ReadFromJsonAsync<AnalyticsDashboardResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dashboard);
        var weekStart = referenceDate.AddDays(-(((int)referenceDate.DayOfWeek + 6) % 7));
        Assert.Equal(referenceDate, dashboard.DailySummary.PeriodStart);
        Assert.Equal(referenceDate, dashboard.DailySummary.PeriodEnd);
        Assert.Equal(0m, dashboard.DailySummary.IncomeTotal);
        Assert.Equal(25m, dashboard.DailySummary.ExpenseTotal);
        Assert.Equal(-25m, dashboard.DailySummary.BalanceDelta);
        Assert.Equal(weekStart, dashboard.WeeklySummary.PeriodStart);
        Assert.Equal(weekStart.AddDays(6), dashboard.WeeklySummary.PeriodEnd);
        Assert.Equal(25m, dashboard.WeeklySummary.ExpenseTotal);
        Assert.Equal(new DateOnly(referenceDate.Year, referenceDate.Month, 1), dashboard.MonthlySummary.PeriodStart);
        Assert.Equal(
            new DateOnly(referenceDate.Year, referenceDate.Month, 1).AddMonths(1).AddDays(-1),
            dashboard.MonthlySummary.PeriodEnd);
        Assert.Equal(25m, dashboard.MonthlySummary.ExpenseTotal);
        Assert.Equal(25m, dashboard.DailyLimit.Spent);
        Assert.Equal(25m, dashboard.DailyLimit.Remaining);
        Assert.Equal(50m, dashboard.LimitsProgress.Daily.Limit);
        Assert.Equal(50m, dashboard.LimitsProgress.Daily.UsedPercent);
        Assert.Equal(100m, dashboard.LimitsProgress.Weekly.Limit);
        Assert.Equal(25m, dashboard.LimitsProgress.Weekly.UsedPercent);
        Assert.Equal(300m, dashboard.LimitsProgress.Monthly.Limit);
        Assert.Equal(8.33m, dashboard.LimitsProgress.Monthly.UsedPercent);
        Assert.Equal(1, dashboard.LimitsProgress.TrackingStreak.CurrentDays);
        Assert.Equal(referenceDate, dashboard.LimitsProgress.TrackingStreak.LastTrackedDate);
        Assert.Single(dashboard.CategoryTotals);
        Assert.Equal(7, dashboard.RecentTrend.Count);
    }

    [Fact]
    public async Task CategoryBreakdown_ReturnsMobileReadyPeriodSharesAndTopCategories()
    {
        const string userId = "synthetic-breakdown-owner";
        var referenceDate = DateOnly.FromDateTime(DateTime.UtcNow);
        using (var scope = factory.Services.CreateScope())
        {
            var projector = scope.ServiceProvider.GetRequiredService<AnalyticsProjector>();
            await projector.ApplyAsync(
                CreateCategoryEvent(
                    userId,
                    "breakdown-income",
                    FinancialRecordEventTypes.IncomeCreated,
                    1000m,
                    "income.salary",
                    referenceDate),
                CancellationToken.None);
            await projector.ApplyAsync(
                CreateCategoryEvent(
                    userId,
                    "breakdown-expense",
                    FinancialRecordEventTypes.ExpenseCreated,
                    100m,
                    "expense.groceries",
                    referenceDate),
                CancellationToken.None);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{AnalyticsApiRoutes.GatewayCategoryBreakdown}?currency=USD&timeZoneId=UTC&referenceDate={referenceDate:yyyy-MM-dd}&period=daily&top=2");
        AddTrustedHeaders(request, userId);
        using var response = await client.SendAsync(request);
        var breakdown =
            await response.Content.ReadFromJsonAsync<AnalyticsCategoryBreakdownResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(breakdown);
        Assert.Equal(AnalyticsBreakdownPeriods.Daily, breakdown.Period);
        Assert.Equal(referenceDate, breakdown.PeriodStart);
        Assert.Equal(referenceDate, breakdown.PeriodEnd);
        Assert.False(breakdown.Freshness.IsStale);
        Assert.NotNull(breakdown.Freshness.LastEventAtUtc);
        Assert.Equal(2, breakdown.Categories.Count);
        Assert.Equal("income.salary", Assert.Single(breakdown.TopIncomeCategories).CategoryId);
        Assert.Equal(
            "expense.groceries",
            Assert.Single(breakdown.TopExpenseCategories).CategoryId);
        Assert.Equal(
            100m,
            breakdown.Categories.Single(item => item.CategoryId == "income.salary")
                .IncomeSharePercent);
        Assert.Equal(
            100m,
            breakdown.Categories.Single(item => item.CategoryId == "expense.groceries")
                .ExpenseSharePercent);
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

        using var unparseableRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{AnalyticsApiRoutes.Dashboard}?currency=USD&timeZoneId=UTC&referenceDate=not-a-date&trendDays=seven");
        AddTrustedHeaders(unparseableRequest, "synthetic-unparseable-owner");
        using var unparseable = await client.SendAsync(unparseableRequest);
        var unparseableProblem =
            await unparseable.Content.ReadFromJsonAsync<AnalyticsApiErrorResponse>();
        Assert.Equal(HttpStatusCode.BadRequest, unparseable.StatusCode);
        Assert.Equal("invalid_analytics_request", unparseableProblem?.Code);

        using var clientLimitRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"{AnalyticsApiRoutes.Dashboard}?currency=USD&timeZoneId=UTC&dailyExpenseLimit=1");
        AddTrustedHeaders(clientLimitRequest, "synthetic-limit-owner");
        using var clientLimit = await client.SendAsync(clientLimitRequest);
        var clientLimitProblem =
            await clientLimit.Content.ReadFromJsonAsync<AnalyticsApiErrorResponse>();
        Assert.Equal(HttpStatusCode.BadRequest, clientLimit.StatusCode);
        Assert.Equal("invalid_analytics_request", clientLimitProblem?.Code);
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

    private static IntegrationEventEnvelope<FinancialRecordChangedV1> CreateCategoryEvent(
        string userId,
        string recordId,
        string eventType,
        decimal amount,
        string categoryId,
        DateOnly date)
    {
        var changedAt = DateTimeOffset.UtcNow;
        return new IntegrationEventEnvelope<FinancialRecordChangedV1>(
            $"synthetic-{recordId}-event",
            $"synthetic-{recordId}-occurrence",
            eventType,
            changedAt,
            eventType == FinancialRecordEventTypes.IncomeCreated
                ? "income-service"
                : "expense-service",
            FinancialRecordEventTypes.SchemaVersion,
            "synthetic-breakdown-correlation",
            "synthetic-breakdown-causation",
            AnalyticsOwnerHasher.Hash(userId),
            new FinancialRecordChangedV1(
                recordId,
                amount,
                "USD",
                categoryId,
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
