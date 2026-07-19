namespace FinancialAssistant.TransactionIntake.Domain.Drafts;

public sealed record ParsedTransactionCandidate(
    string? Type,
    decimal? Amount,
    string? Currency,
    string? CategoryId,
    string? Merchant,
    DateOnly? Date,
    decimal Confidence);
