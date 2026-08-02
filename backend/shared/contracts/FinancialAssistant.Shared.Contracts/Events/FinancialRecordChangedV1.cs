namespace FinancialAssistant.Shared.Contracts.Events;

public static class FinancialRecordEventTypes
{
    public const int SchemaVersion = 1;

    public const string IncomeCreated = "income.created.v1";
    public const string IncomeUpdated = "income.updated.v1";
    public const string IncomeArchived = "income.archived.v1";
    public const string IncomeRestored = "income.restored.v1";

    public const string ExpenseCreated = "expense.created.v1";
    public const string ExpenseUpdated = "expense.updated.v1";
    public const string ExpenseArchived = "expense.archived.v1";
    public const string ExpenseRestored = "expense.restored.v1";
}

/// <summary>
/// Contains the minimum deterministic state required to update financial projections.
/// </summary>
public sealed record FinancialRecordChangedV1(
    string RecordId,
    decimal Amount,
    string Currency,
    string CategoryId,
    DateOnly Date,
    string Status,
    long Revision,
    string Origin,
    DateTimeOffset ChangedAtUtc);
