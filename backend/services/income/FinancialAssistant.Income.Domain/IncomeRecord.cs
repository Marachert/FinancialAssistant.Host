namespace FinancialAssistant.Income.Domain;

public sealed record IncomeRecord(
    string TransactionId,
    string UserId,
    string SourceDraftId,
    decimal Amount,
    string Currency,
    string CategoryId,
    string? Merchant,
    DateOnly Date,
    DateTimeOffset ConfirmedAtUtc);
