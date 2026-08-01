namespace FinancialAssistant.TransactionIntake.Domain.Drafts;

public static class TransactionDraftStatuses
{
    public const string Draft = "draft";
    public const string Confirming = "confirming";
    public const string Confirmed = "confirmed";
    public const string Rejected = "rejected";
}

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
    DateTimeOffset CreatedAtUtc,
    string InputSource = TransactionInputSources.Text,
    string? Note = null,
    string Status = TransactionDraftStatuses.Draft,
    long Revision = 0);
