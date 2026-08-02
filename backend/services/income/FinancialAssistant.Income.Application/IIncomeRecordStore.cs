using FinancialAssistant.Income.Domain;

namespace FinancialAssistant.Income.Application;

public interface IIncomeRecordStore
{
    Task<IncomeRecord> StoreIfMissingAsync(
        IncomeRecord record,
        CancellationToken cancellationToken);

    Task<bool> CreateAsync(
        IncomeRecord record,
        CancellationToken cancellationToken);

    Task<IncomeRecord?> GetAsync(
        string userId,
        string transactionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IncomeRecord>> ListAsync(
        string userId,
        DateOnly from,
        DateOnly to,
        bool includeArchived,
        CancellationToken cancellationToken);

    Task<IncomeRecordMutationResult> ReplaceAsync(
        string userId,
        string transactionId,
        long expectedRevision,
        IncomeRecord replacement,
        CancellationToken cancellationToken);
}

public sealed record IncomeRecordMutationResult(IncomeRecord? Record, bool Replaced);
