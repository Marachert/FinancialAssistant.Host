using FinancialAssistant.Analytics.Application;

namespace FinancialAssistant.Analytics.Infrastructure;

public sealed class InMemoryAnalyticsDailyLimitProvider :
    IAnalyticsDailyLimitProvider,
    IAnalyticsLimitProvider
{
    private readonly object gate = new();
    private readonly Dictionary<string, AnalyticsExpenseLimits> limits =
        new(StringComparer.Ordinal);

    public async Task<decimal?> GetDailyExpenseLimitAsync(
        string userIdHash,
        string currency,
        DateOnly referenceDate,
        CancellationToken cancellationToken) =>
        (await GetExpenseLimitsAsync(
            userIdHash,
            currency,
            referenceDate,
            cancellationToken)).Daily;

    public Task<AnalyticsExpenseLimits> GetExpenseLimitsAsync(
        string userIdHash,
        string currency,
        DateOnly referenceDate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(
                limits.TryGetValue(CreateKey(userIdHash, currency), out var value)
                    ? value
                    : AnalyticsExpenseLimits.Unconfigured);
        }
    }

    public void Set(string userIdHash, string currency, decimal limit) =>
        Set(userIdHash, currency, limit, null, null);

    public void Set(
        string userIdHash,
        string currency,
        decimal? daily,
        decimal? weekly,
        decimal? monthly)
    {
        if (string.IsNullOrWhiteSpace(userIdHash) ||
            string.IsNullOrWhiteSpace(currency) ||
            currency.Length != 3 ||
            daily is <= 0m ||
            weekly is <= 0m ||
            monthly is <= 0m)
        {
            throw new ArgumentException(
                "A valid owner hash, currency, and positive configured limits are required.");
        }

        lock (gate)
        {
            limits[CreateKey(userIdHash, currency)] =
                new AnalyticsExpenseLimits(daily, weekly, monthly);
        }
    }

    private static string CreateKey(string userIdHash, string currency) =>
        $"{userIdHash.Trim()}|{currency.Trim().ToUpperInvariant()}";
}
