namespace FinancialAssistant.AiOrchestration.Contracts;

public static class AiOutputAuthority
{
    public const string Suggestion = "suggestion";
}

public static class NaturalLanguageTransactionFields
{
    public const string Type = "type";
    public const string Amount = "amount";
    public const string Currency = "currency";
    public const string Date = "date";
    public const string Merchant = "merchant";
    public const string Category = "category";
    public const string Note = "note";
}

public sealed record NaturalLanguageTransactionParseRequest(
    string Input,
    string? Locale = null,
    string? TimeZone = null,
    string? DefaultCurrency = null);

public sealed record NaturalLanguageTransactionSuggestion(
    string? Type,
    decimal? Amount,
    string? Currency,
    DateOnly? Date,
    string? Merchant,
    string? CategoryId,
    string? Note);

public sealed record NaturalLanguageTransactionConfidenceScores(
    decimal Overall,
    decimal? Type,
    decimal? Amount,
    decimal? Currency,
    decimal? Date,
    decimal? Merchant,
    decimal? Category,
    decimal? Note);

public sealed record NaturalLanguageTransactionAmbiguity(
    string Code,
    string Field,
    IReadOnlyList<string> CandidateValues);

public sealed record NaturalLanguageTransactionParseResponse(
    string CallId,
    NaturalLanguageTransactionSuggestion Suggestion,
    NaturalLanguageTransactionConfidenceScores Confidence,
    IReadOnlyList<NaturalLanguageTransactionAmbiguity> Ambiguities,
    IReadOnlyList<string> MissingFields,
    string Explanation)
{
    public string OutputAuthority => AiOutputAuthority.Suggestion;

    public bool RequiresReview => true;
}
