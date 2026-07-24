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
        DateTimeOffset createdAtUtc,
        TransactionDraftSuggestionContext? suggestionContext = null)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        suggestionContext ??= TransactionDraftSuggestionContext.AiNaturalLanguage;
        var source = NormalizeSource(suggestionContext.Source);
        var sourceReferenceId = NormalizeSourceReferenceId(suggestionContext.SourceReferenceId);
        var sourceAmbiguities = NormalizeCodes(
            suggestionContext.Ambiguities,
            nameof(suggestionContext.Ambiguities));
        var sourceMissingFields = NormalizeCodes(
            suggestionContext.MissingFields,
            nameof(suggestionContext.MissingFields));
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

        ambiguities.UnionWith(sourceAmbiguities);
        var missingFields = new SortedSet<string>(sourceMissingFields, StringComparer.Ordinal);
        AddMissingFields(type, amount, currency, categoryId, date, ambiguities, missingFields);
        var requiresReview = ambiguities.Count > 0 || missingFields.Count > 0;
        var reviewMessage = NormalizeReviewMessage(
            suggestionContext.ReviewMessage,
            confidence,
            ambiguities.Count,
            missingFields.Count);
        var suggestion = new TransactionDraftSuggestionMetadata(
            source,
            sourceReferenceId,
            confidence,
            ambiguities.ToArray(),
            missingFields.ToArray(),
            reviewMessage);

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
            requiresReview,
            suggestion,
            createdAtUtc);
    }

    private static string NormalizeSource(string source)
    {
        var normalized = source?.Trim().ToLowerInvariant();
        return normalized is
            TransactionDraftSuggestionSources.AiNaturalLanguage or
            TransactionDraftSuggestionSources.ReceiptOcr
            ? normalized
            : throw new ArgumentException("Suggestion source is invalid.", nameof(source));
    }

    private static string? NormalizeSourceReferenceId(string? sourceReferenceId)
    {
        var normalized = sourceReferenceId?.Trim();
        if (normalized?.Length > 100)
        {
            throw new ArgumentException(
                "Suggestion source reference cannot exceed 100 characters.",
                nameof(sourceReferenceId));
        }

        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static string[] NormalizeCodes(
        IReadOnlyList<string> values,
        string parameterName)
    {
        if (values is null || values.Count > 50)
        {
            throw new ArgumentException("Suggestion metadata is invalid.", parameterName);
        }

        var normalized = values
            .Select(value => value?.Trim().ToLowerInvariant())
            .ToArray();
        if (normalized.Any(value =>
                string.IsNullOrWhiteSpace(value) ||
                value.Length > 64 ||
                value.Any(character =>
                    !(char.IsLower(character) || char.IsDigit(character) || character == '_'))))
        {
            throw new ArgumentException("Suggestion metadata is invalid.", parameterName);
        }

        return normalized
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddMissingFields(
        string type,
        decimal? amount,
        string? currency,
        string? categoryId,
        DateOnly? date,
        IReadOnlySet<string> ambiguities,
        ISet<string> missingFields)
    {
        AddMissingField(type == TransactionTypes.Unknown, "type", missingFields);
        AddMissingField(amount is null, "amount", missingFields);
        AddMissingField(currency is null, "currency", missingFields);
        AddMissingField(
            type != TransactionTypes.Transfer && categoryId is null,
            "category",
            missingFields);
        AddMissingField(date is null, "date", missingFields);
        AddMissingField(ambiguities.Contains("merchant"), "merchant", missingFields);
    }

    private static void AddMissingField(
        bool isMissing,
        string field,
        ISet<string> missingFields)
    {
        if (isMissing)
        {
            missingFields.Add(field);
        }
    }

    private static string NormalizeReviewMessage(
        string? reviewMessage,
        decimal confidence,
        int ambiguityCount,
        int missingFieldCount)
    {
        var normalized = string.IsNullOrWhiteSpace(reviewMessage)
            ? null
            : string.Join(
                ' ',
                reviewMessage.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized?.Length > 280)
        {
            throw new ArgumentException(
                "Suggestion review message cannot exceed 280 characters.",
                nameof(reviewMessage));
        }

        if (normalized is not null)
        {
            return normalized;
        }

        if (confidence < 0.75m)
        {
            return "Confidence is low. Review the suggested fields before confirming.";
        }

        if (missingFieldCount > 0)
        {
            return "Complete the missing fields and review the suggestion before confirming.";
        }

        if (ambiguityCount > 0)
        {
            return "Resolve the ambiguous fields and review the suggestion before confirming.";
        }

        return "Review the suggested transaction before confirming.";
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

        var rounded = decimal.Round(value.Value, 2, MidpointRounding.ToEven);
        if (rounded <= 0)
        {
            ambiguities.Add("amount");
            return null;
        }

        return rounded;
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
