using System.Text.Json;
using FinancialAssistant.Analytics.Contracts;
using Xunit;

namespace FinancialAssistant.Analytics.Tests;

public sealed class DashboardCompositionContractTests
{
    [Fact]
    public void EmptyDashboard_IsExplicitAndMobileSerializable()
    {
        var date = new DateOnly(2026, 8, 9);
        var generatedAt = new DateTimeOffset(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);
        var emptyPeriod = new DashboardPeriodWidgetResponse(date, date, 0m, 0m, 0m);
        var unconfigured = new DashboardLimitWidgetItemResponse(
            false,
            null,
            0m,
            null,
            null);
        var unavailable = new DashboardSourceFreshnessResponse(false, false, null);
        var response = new DashboardCompositionResponse(
            DashboardContractVersions.V1,
            "USD",
            "Etc/UTC",
            date,
            generatedAt,
            new DashboardSummaryWidgetResponse(emptyPeriod, emptyPeriod, emptyPeriod),
            new DashboardCategoryWidgetResponse(
                Array.Empty<DashboardCategoryItemResponse>(),
                false),
            new DashboardScoreWidgetResponse(false, null, null, null),
            new DashboardLimitsWidgetResponse(
                unconfigured,
                unconfigured,
                unconfigured,
                0,
                "Add a confirmed record to start a tracking streak."),
            new DashboardRecommendationWidgetResponse(
                Array.Empty<DashboardRecommendationPreviewItemResponse>(),
                false),
            new DashboardNotificationBadgeResponse(0, false),
            new DashboardEmptyStateResponse(false, false, false, false, false),
            new DashboardFreshnessResponse(
                new DashboardSourceFreshnessResponse(true, false, generatedAt),
                unavailable,
                unavailable,
                unavailable));

        var json = JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"schemaVersion\":\"1\"", json, StringComparison.Ordinal);
        Assert.Contains("\"items\":[]", json, StringComparison.Ordinal);
        Assert.Contains("\"hasFinancialData\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"freshness\":", json, StringComparison.Ordinal);
        Assert.Contains("\"score\":{\"isAvailable\":false", json, StringComparison.Ordinal);
        Assert.DoesNotContain("userIdHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eventId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("revision", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("storage", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PopulatedDashboard_ExposesStableWidgetShapesAndPerSourceFreshness()
    {
        var date = new DateOnly(2026, 8, 9);
        var generatedAt = new DateTimeOffset(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);
        var response = new DashboardCompositionResponse(
            DashboardContractVersions.V1,
            "USD",
            "Etc/UTC",
            date,
            generatedAt,
            new DashboardSummaryWidgetResponse(
                new(date, date, 100m, 25m, 75m),
                new(date.AddDays(-5), date.AddDays(1), 500m, 225m, 275m),
                new(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31), 900m, 400m, 500m)),
            new DashboardCategoryWidgetResponse(
                [new("expense.groceries", 100m, 25m)],
                true),
            new DashboardScoreWidgetResponse(
                true,
                72,
                "v1",
                new DateTimeOffset(2026, 8, 9, 9, 0, 0, TimeSpan.Zero)),
            new DashboardLimitsWidgetResponse(
                new(true, 50m, 25m, 25m, 50m),
                new(true, 300m, 225m, 75m, 75m),
                new(true, 1_000m, 400m, 600m, 40m),
                3,
                "Nice progress. Keep tracking each day."),
            new DashboardRecommendationWidgetResponse(
                [new("recommendation-1", "spending-pressure", "warning", "Review spending", "Open the recommendation for details.")],
                false),
            new DashboardNotificationBadgeResponse(2, true),
            new DashboardEmptyStateResponse(true, true, true, true, true),
            new DashboardFreshnessResponse(
                new(true, false, generatedAt),
                new(true, true, generatedAt.AddHours(-1)),
                new(true, false, generatedAt.AddMinutes(-15)),
                new(false, false, null)));

        Assert.Equal(DashboardContractVersions.V1, response.SchemaVersion);
        Assert.Equal(72, response.Score.Score);
        Assert.Single(response.Categories.TopExpenseCategories);
        Assert.Single(response.Recommendations.Items);
        Assert.True(response.Notifications.HasUnread);
        Assert.True(response.EmptyState.HasFinancialData);
        Assert.False(response.Freshness.Analytics.IsStale);
        Assert.True(response.Freshness.Score.IsStale);
        Assert.False(response.Freshness.Notifications.IsAvailable);
        Assert.Null(response.Freshness.Notifications.LastSuccessfulUpdateAtUtc);
    }
}
