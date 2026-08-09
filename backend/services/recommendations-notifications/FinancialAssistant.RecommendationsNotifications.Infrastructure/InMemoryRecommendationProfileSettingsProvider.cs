using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public sealed class InMemoryRecommendationProfileSettingsProvider :
    IRecommendationProfileSettingsProvider
{
    private readonly object gate = new();
    private readonly Dictionary<string, RecommendationProfileSettings> settings =
        new(StringComparer.Ordinal);

    public Task<RecommendationProfileSettings> GetAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(
                settings.TryGetValue(CreateKey(userIdHash, currency), out var value)
                    ? value
                    : RecommendationProfileSettings.Unavailable);
        }
    }

    public void Set(
        string userIdHash,
        string currency,
        RecommendationProfileSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.MonthlyBudgetLimit is <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Monthly budget limit must be positive when supplied.");
        }

        lock (gate)
        {
            settings[CreateKey(userIdHash, currency)] = value;
        }
    }

    private static string CreateKey(string userIdHash, string currency)
    {
        if (string.IsNullOrWhiteSpace(userIdHash))
        {
            throw new ArgumentException("Owner hash is required.", nameof(userIdHash));
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new ArgumentException(
                "Currency must use a three-letter code.",
                nameof(currency));
        }

        return $"{userIdHash.Trim()}|{currency.Trim().ToUpperInvariant()}";
    }
}
