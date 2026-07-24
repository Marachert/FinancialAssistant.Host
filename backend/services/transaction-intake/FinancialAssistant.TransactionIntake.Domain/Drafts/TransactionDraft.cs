namespace FinancialAssistant.TransactionIntake.Domain.Drafts;

public sealed record TransactionDraft(
    string Id,
    string UserId,
    string InputFingerprint,
    string Type,
    decimal? Amount,
    string? Currency,
    string? CategoryId,
    string? Merchant,
    DateOnly? Date,
    decimal Confidence,
    IReadOnlyList<string> Ambiguities,
    bool RequiresReview,
    TransactionDraftSuggestionMetadata Suggestion,
    DateTimeOffset CreatedAtUtc)
{
    public const string Status = "draft";
}
