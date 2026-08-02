using System.Text.Json;
using FinancialAssistant.Analytics.Contracts;
using Xunit;

namespace FinancialAssistant.Analytics.Tests;

public sealed class AnalyticsContractTests
{
    [Fact]
    public void DashboardContract_IsStableAndPrivacySafe()
    {
        var response = new AnalyticsDashboardResponse(
            "USD",
            "UTC",
            new DateOnly(2026, 8, 20),
            new AnalyticsPeriodSummaryResponse(
                new DateOnly(2026, 8, 20),
                new DateOnly(2026, 8, 20),
                500m,
                25m,
                475m),
            new AnalyticsPeriodSummaryResponse(
                new DateOnly(2026, 8, 17),
                new DateOnly(2026, 8, 23),
                500m,
                100m,
                400m),
            new AnalyticsPeriodSummaryResponse(
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 31),
                500m,
                100m,
                400m),
            new AnalyticsDailyLimitResponse(true, 50m, 25m, 25m, 50m),
            new AnalyticsMonthlyProgressResponse(500m, 100m, 400m, 20m),
            new[] { new AnalyticsCategoryTotalResponse("expense.groceries", 0m, 100m, -100m) },
            new[] { new AnalyticsTrendPointResponse(new DateOnly(2026, 8, 20), 0m, 25m, -25m) },
            new AnalyticsFreshnessResponse(false, DateTimeOffset.Parse("2026-08-20T12:00:00Z")));

        var json = JsonSerializer.Serialize(response);

        Assert.Equal("/api/v1/analytics/dashboard", AnalyticsApiRoutes.Dashboard);
        Assert.Equal("/analytics/dashboard", AnalyticsApiRoutes.GatewayDashboard);
        Assert.Contains("DailySummary", json, StringComparison.Ordinal);
        Assert.Contains("WeeklySummary", json, StringComparison.Ordinal);
        Assert.Contains("MonthlySummary", json, StringComparison.Ordinal);
        Assert.DoesNotContain("UserIdHash", json, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("EventId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Revision", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Origin", json, StringComparison.Ordinal);
    }

    [Fact]
    public void CategoryBreakdownContract_IsStableAndPrivacySafe()
    {
        var item = new AnalyticsCategoryBreakdownItemResponse(
            "expense.groceries",
            0m,
            100m,
            -100m,
            0m,
            100m);
        var response = new AnalyticsCategoryBreakdownResponse(
            "USD",
            "UTC",
            new DateOnly(2026, 8, 20),
            AnalyticsBreakdownPeriods.Daily,
            new DateOnly(2026, 8, 20),
            new DateOnly(2026, 8, 20),
            new[] { item },
            Array.Empty<AnalyticsCategoryBreakdownItemResponse>(),
            new[] { item },
            new AnalyticsFreshnessResponse(false, DateTimeOffset.UtcNow));

        var json = JsonSerializer.Serialize(response);

        Assert.Equal(
            "/api/v1/analytics/category-breakdown",
            AnalyticsApiRoutes.CategoryBreakdown);
        Assert.Equal(
            "/analytics/category-breakdown",
            AnalyticsApiRoutes.GatewayCategoryBreakdown);
        Assert.Contains("ExpenseSharePercent", json, StringComparison.Ordinal);
        Assert.Contains("TopExpenseCategories", json, StringComparison.Ordinal);
        Assert.Contains("Freshness", json, StringComparison.Ordinal);
        Assert.Contains("IsStale", json, StringComparison.Ordinal);
        Assert.DoesNotContain("UserIdHash", json, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordId", json, StringComparison.Ordinal);
    }
}
