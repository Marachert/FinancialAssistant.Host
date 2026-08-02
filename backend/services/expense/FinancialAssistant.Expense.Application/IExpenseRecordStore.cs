using FinancialAssistant.Expense.Domain;

namespace FinancialAssistant.Expense.Application;

public interface IExpenseRecordStore
{
    Task<ExpenseRecord> StoreIfMissingAsync(
        ExpenseRecord record,
        CancellationToken cancellationToken);

    Task<bool> CreateAsync(
        ExpenseRecord record,
        CancellationToken cancellationToken);

    Task<ExpenseRecord?> GetAsync(
        string userId,
        string transactionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ExpenseRecord>> ListAsync(
        string userId,
        DateOnly from,
        DateOnly to,
        bool includeArchived,
        CancellationToken cancellationToken);

    Task<ExpenseRecordMutationResult> ReplaceAsync(
        string userId,
        string transactionId,
        long expectedRevision,
        ExpenseRecord replacement,
        CancellationToken cancellationToken);
}

public sealed record ExpenseRecordMutationResult(ExpenseRecord? Record, bool Replaced);
