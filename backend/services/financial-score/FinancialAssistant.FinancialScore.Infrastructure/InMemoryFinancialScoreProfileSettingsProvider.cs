using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Domain;

namespace FinancialAssistant.FinancialScore.Infrastructure;

public sealed class InMemoryFinancialScoreProfileSettingsProvider :
    IFinancialScoreProfileSettingsProvider
{
    private readonly object gate = new();
    private readonly Dictionary<string, FinancialScoreProfileSettings> settings =
        new(StringComparer.Ordinal);

    public Task<FinancialScoreProfileSettings> GetAsync(
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
                    : FinancialScoreProfileSettings.Unconfigured);
        }
    }

    public void Set(
        string userIdHash,
        string currency,
        FinancialScoreProfileSettings value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.MonthlyBudgetAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Monthly budget amount cannot be negative.");
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
