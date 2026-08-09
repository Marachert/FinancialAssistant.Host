using System.Text.Json;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Contracts;
using Xunit;

namespace FinancialAssistant.Analytics.Tests;

public sealed class AnalyticsRebuildPlannerTests
{
    private const string OwnerScopeHash =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private static readonly DateTimeOffset RequestedAt =
        new(2026, 8, 9, 11, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ProducesStableOwnerPeriodSourceJobKeyAndOrderedStages()
    {
        var planner = new AnalyticsRebuildPlanner();
        var first = planner.Create(Request());
        var retry = planner.Create(
            Request(requestedAtUtc: RequestedAt.AddMinutes(5)));

        Assert.Equal(first.JobKey, retry.JobKey);
        Assert.StartsWith("analytics-rebuild-", first.JobKey, StringComparison.Ordinal);
        Assert.Equal(AnalyticsRebuildContractVersions.V1, first.ContractVersion);
        Assert.Equal(new DateOnly(2026, 1, 1), first.Scope.PeriodStart);
        Assert.Equal(new DateOnly(2026, 3, 31), first.Scope.PeriodEnd);
        Assert.Equal(
            new[]
            {
                AnalyticsRebuildStages.ValidateSource,
                AnalyticsRebuildStages.RebuildAnalytics,
                AnalyticsRebuildStages.RebuildScoreHistory,
                AnalyticsRebuildStages.RefreshLimitProgress,
                AnalyticsRebuildStages.RefreshRecommendationInputs,
                AnalyticsRebuildStages.VerifyAndSwap
            },
            first.OrderedStages);
    }

    [Fact]
    public void Create_ChangesJobKeyWhenScopeOrSourceSnapshotChanges()
    {
        var planner = new AnalyticsRebuildPlanner();

        var baseline = planner.Create(Request());
        var changedPeriod = planner.Create(
            Request(periodEnd: new DateOnly(2026, 4, 1)));
        var changedSource = planner.Create(
            Request(sourceSnapshotVersion: "confirmed-records-v43"));

        Assert.NotEqual(baseline.JobKey, changedPeriod.JobKey);
        Assert.NotEqual(baseline.JobKey, changedSource.JobKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("owner@example.com")]
    [InlineData("abc123")]
    public void Create_RejectsNonPseudonymousOwnerScope(string ownerScopeHash)
    {
        var planner = new AnalyticsRebuildPlanner();

        Assert.Throws<ArgumentException>(
            () => planner.Create(Request(ownerScopeHash: ownerScopeHash)));
    }

    [Fact]
    public void Create_RejectsInvalidOrUnboundedPeriodsAndNonUtcTimestamps()
    {
        var planner = new AnalyticsRebuildPlanner();

        Assert.Throws<ArgumentException>(
            () => planner.Create(
                Request(
                    periodStart: new DateOnly(2026, 4, 1),
                    periodEnd: new DateOnly(2026, 3, 31))));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => planner.Create(
                Request(
                    periodStart: new DateOnly(2010, 1, 1),
                    periodEnd: new DateOnly(2026, 1, 1))));
        Assert.Throws<ArgumentException>(
            () => planner.Create(
                Request(
                    requestedAtUtc: new DateTimeOffset(
                        2026,
                        8,
                        9,
                        14,
                        30,
                        0,
                        TimeSpan.FromHours(3)))));
    }

    [Fact]
    public void ProgressContract_ExposesSafeFailureEvidenceWithoutOwnerScope()
    {
        var progress = new AnalyticsRebuildProgressResponse(
            AnalyticsRebuildContractVersions.V1,
            "analytics-rebuild-synthetic",
            AnalyticsRebuildJobStatuses.Failed,
            AnalyticsRebuildStages.RebuildScoreHistory,
            125,
            200,
            RequestedAt,
            RequestedAt.AddSeconds(1),
            RequestedAt.AddMinutes(2),
            RequestedAt.AddMinutes(2),
            new AnalyticsRebuildFailureResponse(
                "score_rebuild_failed",
                "A downstream deterministic projection stage failed.",
                AnalyticsRebuildStages.RebuildScoreHistory,
                RequestedAt.AddMinutes(2)));

        var json = JsonSerializer.Serialize(
            progress,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"status\":\"failed\"", json, StringComparison.Ordinal);
        Assert.Contains("\"processedRecords\":125", json, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"score_rebuild_failed\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain(OwnerScopeHash, json, StringComparison.Ordinal);
        Assert.DoesNotContain("amount", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payload", json, StringComparison.OrdinalIgnoreCase);
    }

    private static AnalyticsRebuildRequest Request(
        string ownerScopeHash = OwnerScopeHash,
        DateOnly? periodStart = null,
        DateOnly? periodEnd = null,
        string sourceSnapshotVersion = "confirmed-records-v42",
        DateTimeOffset? requestedAtUtc = null) =>
        new(
            ownerScopeHash,
            periodStart ?? new DateOnly(2026, 1, 1),
            periodEnd ?? new DateOnly(2026, 3, 31),
            sourceSnapshotVersion,
            requestedAtUtc ?? RequestedAt);
}
