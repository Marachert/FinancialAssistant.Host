namespace FinancialAssistant.TransactionIntake.Contracts;

public sealed record TransactionDraftResponse(
    string Id,
    string Status,
    string Type,
    decimal? Amount,
    string? Currency,
    string? CategoryId,
    string? Merchant,
    DateOnly? Date,
    decimal Confidence,
    IReadOnlyList<string> Ambiguities,
    bool RequiresReview,
    DateTimeOffset CreatedAtUtc);
