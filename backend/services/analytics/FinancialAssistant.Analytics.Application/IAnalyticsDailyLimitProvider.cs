namespace FinancialAssistant.Analytics.Application;

public interface IAnalyticsDailyLimitProvider
{
    Task<decimal?> GetDailyExpenseLimitAsync(
        string userIdHash,
        string currency,
        DateOnly referenceDate,
        CancellationToken cancellationToken);
}

public sealed record AnalyticsExpenseLimits(
    decimal? Daily,
    decimal? Weekly,
    decimal? Monthly)
{
    public static AnalyticsExpenseLimits Unconfigured { get; } =
        new(null, null, null);
}

public interface IAnalyticsLimitProvider
{
    Task<AnalyticsExpenseLimits> GetExpenseLimitsAsync(
        string userIdHash,
        string currency,
        DateOnly referenceDate,
        CancellationToken cancellationToken);
}
