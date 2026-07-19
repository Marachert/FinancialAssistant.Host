namespace FinancialAssistant.TransactionIntake.Contracts;

public sealed record ConfirmedTransactionResponse(
    string TransactionId,
    string DraftId,
    string Status,
    string TransactionType,
    decimal Amount,
    string Currency,
    string CategoryId,
    string? Merchant,
    DateOnly Date,
    DateTimeOffset ConfirmedAtUtc);
