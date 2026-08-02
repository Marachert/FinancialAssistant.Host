namespace FinancialAssistant.Expense.Domain;

public static class ExpenseRecordStatuses
{
    public const string Active = "active";
    public const string Archived = "archived";
}

public static class ExpenseRecordOrigins
{
    public const string ConfirmedTransaction = "confirmed_transaction";
    public const string Manual = "manual";
}

public sealed record ExpenseRecord(
    string TransactionId,
    string UserId,
    string? SourceDraftId,
    decimal Amount,
    string Currency,
    string CategoryId,
    string? Merchant,
    DateOnly Date,
    DateTimeOffset ConfirmedAtUtc,
    string Status = ExpenseRecordStatuses.Active,
    long Revision = 0,
    DateTimeOffset? UpdatedAtUtc = null,
    string Origin = ExpenseRecordOrigins.ConfirmedTransaction);
