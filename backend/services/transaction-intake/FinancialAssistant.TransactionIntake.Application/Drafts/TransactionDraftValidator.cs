using System.Text.RegularExpressions;
using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Application.Drafts;

public sealed partial class TransactionDraftValidator
{
    private const decimal MaximumAmount = 999_999_999_999.99m;

    private static readonly HashSet<string> SupportedCurrencies =
        new(StringComparer.Ordinal) { "EUR", "GBP", "UAH", "USD" };

    public TransactionDraft Validate(
        string draftId,
        string userId,
        string inputFingerprint,
        ParsedTransactionCandidate candidate,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var ambiguities = new SortedSet<string>(StringComparer.Ordinal);
        var type = NormalizeType(candidate.Type, ambiguities);
        var amount = NormalizeAmount(candidate.Amount, ambiguities);
        var currency = NormalizeCurrency(candidate.Currency, ambiguities);
        var categoryId = NormalizeCategory(candidate.CategoryId, type, ambiguities);
        var merchant = NormalizeMerchant(candidate.Merchant, ambiguities);
        var date = NormalizeDate(candidate.Date, createdAtUtc, ambiguities);
        var confidence = candidate.Confidence is >= 0 and <= 1 ? candidate.Confidence : 0;

        if (confidence < 0.75m)
        {
            ambiguities.Add("low_confidence");
        }

        return new TransactionDraft(
            draftId,
            userId,
            inputFingerprint,
            type,
            amount,
            currency,
            categoryId,
            merchant,
            date,
            confidence,
            ambiguities.ToArray(),
            ambiguities.Count > 0,
            createdAtUtc);
    }

    private static string NormalizeType(string? value, ISet<string> ambiguities)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? TransactionTypes.Unknown;
        if (!TransactionTypes.IsSupported(normalized) || normalized == TransactionTypes.Unknown)
        {
            ambiguities.Add("type");
            return TransactionTypes.Unknown;
        }

        return normalized;
    }

    private static decimal? NormalizeAmount(decimal? value, ISet<string> ambiguities)
    {
        if (value is null or <= 0 or > MaximumAmount)
        {
            ambiguities.Add("amount");
            return null;
        }

        return decimal.Round(value.Value, 2, MidpointRounding.ToEven);
    }

    private static string? NormalizeCurrency(string? value, ISet<string> ambiguities)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        if (normalized is null || !SupportedCurrencies.Contains(normalized))
        {
            ambiguities.Add("currency");
            return null;
        }

        return normalized;
    }

    private static string? NormalizeCategory(
        string? value,
        string type,
        ISet<string> ambiguities)
    {
        if (type == TransactionTypes.Transfer)
        {
            return null;
        }

        var normalized = value?.Trim().ToLowerInvariant();
        if (normalized is null)
        {
            ambiguities.Add("category");
            return null;
        }

        var expectedPrefix = type == TransactionTypes.Income ? "income." : "expense.";
        if (type == TransactionTypes.Unknown ||
            !CategoryIdPattern().IsMatch(normalized) ||
            !normalized.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            ambiguities.Add("category");
            return null;
        }

        return normalized;
    }

    private static string? NormalizeMerchant(string? value, ISet<string> ambiguities)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length > 120)
        {
            ambiguities.Add("merchant");
            return null;
        }

        return normalized;
    }

    private static DateOnly? NormalizeDate(
        DateOnly? value,
        DateTimeOffset createdAtUtc,
        ISet<string> ambiguities)
    {
        if (value is null)
        {
            ambiguities.Add("date");
            return null;
        }

        var currentDate = DateOnly.FromDateTime(createdAtUtc.UtcDateTime);
        if (value < currentDate.AddYears(-10) || value > currentDate.AddDays(366))
        {
            ambiguities.Add("date");
            return null;
        }

        return value;
    }

    [GeneratedRegex("^(?:income|expense)\\.[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CategoryIdPattern();
}
