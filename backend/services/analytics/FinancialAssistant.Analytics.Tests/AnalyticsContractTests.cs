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
            new AnalyticsDailyLimitResponse(true, 50m, 25m, 25m, 50m),
            new AnalyticsMonthlyProgressResponse(500m, 100m, 400m, 20m),
            new[] { new AnalyticsCategoryTotalResponse("expense.groceries", 0m, 100m, -100m) },
            new[] { new AnalyticsTrendPointResponse(new DateOnly(2026, 8, 20), 0m, 25m, -25m) },
            new AnalyticsFreshnessResponse(false, DateTimeOffset.Parse("2026-08-20T12:00:00Z")));

        var json = JsonSerializer.Serialize(response);

        Assert.Equal("/api/v1/analytics/dashboard", AnalyticsApiRoutes.Dashboard);
        Assert.Equal("/analytics/dashboard", AnalyticsApiRoutes.GatewayDashboard);
        Assert.DoesNotContain("UserIdHash", json, StringComparison.Ordinal);
        Assert.DoesNotContain("RecordId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("EventId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Revision", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Origin", json, StringComparison.Ordinal);
    }
}
