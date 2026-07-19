namespace FinancialAssistant.Expense.Domain;

public sealed record ExpenseRecord(
    string TransactionId,
    string UserId,
    string SourceDraftId,
    decimal Amount,
    string Currency,
    string CategoryId,
    string? Merchant,
    DateOnly Date,
    DateTimeOffset ConfirmedAtUtc);
