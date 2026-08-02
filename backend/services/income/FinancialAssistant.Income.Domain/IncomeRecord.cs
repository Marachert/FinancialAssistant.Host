namespace FinancialAssistant.Income.Domain;

public static class IncomeRecordStatuses
{
    public const string Active = "active";
    public const string Archived = "archived";
}

public static class IncomeRecordOrigins
{
    public const string ConfirmedTransaction = "confirmed_transaction";
    public const string Manual = "manual";
}

public sealed record IncomeRecord(
    string TransactionId,
    string UserId,
    string? SourceDraftId,
    decimal Amount,
    string Currency,
    string CategoryId,
    string? Merchant,
    DateOnly Date,
    DateTimeOffset ConfirmedAtUtc,
    string Status = IncomeRecordStatuses.Active,
    long Revision = 0,
    DateTimeOffset? UpdatedAtUtc = null,
    string Origin = IncomeRecordOrigins.ConfirmedTransaction);
