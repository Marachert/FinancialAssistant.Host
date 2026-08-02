using FinancialAssistant.Analytics.Application;

namespace FinancialAssistant.Analytics.Infrastructure;

public sealed class InMemoryAnalyticsDailyLimitProvider : IAnalyticsDailyLimitProvider
{
    private readonly object gate = new();
    private readonly Dictionary<string, decimal> limits = new(StringComparer.Ordinal);

    public Task<decimal?> GetDailyExpenseLimitAsync(
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
                    ? (decimal?)value
                    : null);
        }
    }

    public void Set(string userIdHash, string currency, decimal limit)
    {
        if (string.IsNullOrWhiteSpace(userIdHash) ||
            string.IsNullOrWhiteSpace(currency) ||
            currency.Length != 3 ||
            limit <= 0m)
        {
            throw new ArgumentException("A valid owner hash, currency, and positive limit are required.");
        }

        lock (gate)
        {
            limits[CreateKey(userIdHash, currency)] = limit;
        }
    }

    private static string CreateKey(string userIdHash, string currency) =>
        $"{userIdHash.Trim()}|{currency.Trim().ToUpperInvariant()}";
}
