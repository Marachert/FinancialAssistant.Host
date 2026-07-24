namespace FinancialAssistant.TransactionIntake.Domain.Drafts;

public static class TransactionDraftSuggestionSources
{
    public const string AiNaturalLanguage = "ai_natural_language";
    public const string ReceiptOcr = "receipt_ocr";
}

public static class TransactionDraftSuggestionAuthority
{
    public const string Suggestion = "suggestion";
}

public sealed record TransactionDraftSuggestionContext(
    string Source,
    string? SourceReferenceId,
    IReadOnlyList<string> Ambiguities,
    IReadOnlyList<string> MissingFields,
    string? ReviewMessage = null)
{
    public static TransactionDraftSuggestionContext AiNaturalLanguage { get; } =
        new(
            TransactionDraftSuggestionSources.AiNaturalLanguage,
            SourceReferenceId: null,
            Ambiguities: Array.Empty<string>(),
            MissingFields: Array.Empty<string>());
}

public sealed record TransactionDraftSuggestionMetadata(
    string Source,
    string? SourceReferenceId,
    decimal Confidence,
    IReadOnlyList<string> Ambiguities,
    IReadOnlyList<string> MissingFields,
    string ReviewMessage)
{
    public string OutputAuthority => TransactionDraftSuggestionAuthority.Suggestion;
}
