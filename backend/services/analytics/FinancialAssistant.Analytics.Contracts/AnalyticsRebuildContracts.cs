namespace FinancialAssistant.Analytics.Contracts;

public static class AnalyticsRebuildContractVersions
{
    public const string V1 = "1";
}

public static class AnalyticsRebuildJobStatuses
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
}

public static class AnalyticsRebuildStages
{
    public const string ValidateSource = "validate-source";
    public const string RebuildAnalytics = "rebuild-analytics";
    public const string RebuildScoreHistory = "rebuild-score-history";
    public const string RefreshLimitProgress = "refresh-limit-progress";
    public const string RefreshRecommendationInputs = "refresh-recommendation-inputs";
    public const string VerifyAndSwap = "verify-and-swap";

    public static IReadOnlyList<string> Ordered { get; } =
    [
        ValidateSource,
        RebuildAnalytics,
        RebuildScoreHistory,
        RefreshLimitProgress,
        RefreshRecommendationInputs,
        VerifyAndSwap
    ];
}

public sealed record AnalyticsRebuildRequest(
    string OwnerScopeHash,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string SourceSnapshotVersion,
    DateTimeOffset RequestedAtUtc);

public sealed record AnalyticsRebuildScopeResponse(
    DateOnly PeriodStart,
    DateOnly PeriodEnd);

public sealed record AnalyticsRebuildPlanResponse(
    string ContractVersion,
    string JobKey,
    AnalyticsRebuildScopeResponse Scope,
    string SourceSnapshotVersion,
    IReadOnlyList<string> OrderedStages);

public sealed record AnalyticsRebuildFailureResponse(
    string Code,
    string SafeDetail,
    string FailedStage,
    DateTimeOffset FailedAtUtc);

public sealed record AnalyticsRebuildProgressResponse(
    string ContractVersion,
    string JobKey,
    string Status,
    string? CurrentStage,
    long ProcessedRecords,
    long? TotalRecords,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    AnalyticsRebuildFailureResponse? Failure);
