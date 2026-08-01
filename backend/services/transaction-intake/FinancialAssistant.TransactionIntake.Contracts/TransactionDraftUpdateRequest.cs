namespace FinancialAssistant.TransactionIntake.Contracts;

public sealed record TransactionDraftUpdateRequest(
    string? Type,
    decimal? Amount,
    string? Currency,
    string? CategoryId,
    string? Merchant,
    DateOnly? Date,
    string? Note = null);
