using System.Collections.Concurrent;
using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Domain;

namespace FinancialAssistant.Expense.Infrastructure;

public sealed class InMemoryExpenseRecordStore : IExpenseRecordStore
{
    private readonly ConcurrentDictionary<string, ExpenseRecord> records =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<ExpenseRecord> Records => records.Values.ToArray();

    public Task<ExpenseRecord> StoreIfMissingAsync(
        ExpenseRecord record,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(records.GetOrAdd(record.TransactionId, record));
    }

    public Task<bool> CreateAsync(
        ExpenseRecord record,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(records.TryAdd(record.TransactionId, record));
    }

    public Task<ExpenseRecord?> GetAsync(
        string userId,
        string transactionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        records.TryGetValue(transactionId, out var record);
        return Task.FromResult(
            record is not null &&
            string.Equals(record.UserId, userId, StringComparison.Ordinal)
                ? record
                : null);
    }

    public Task<IReadOnlyList<ExpenseRecord>> ListAsync(
        string userId,
        DateOnly from,
        DateOnly to,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ExpenseRecord> result = records.Values
            .Where(record =>
                string.Equals(record.UserId, userId, StringComparison.Ordinal) &&
                record.Date >= from &&
                record.Date <= to &&
                (includeArchived || record.Status == ExpenseRecordStatuses.Active))
            .OrderByDescending(record => record.Date)
            .ThenBy(record => record.TransactionId, StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<ExpenseRecordMutationResult> ReplaceAsync(
        string userId,
        string transactionId,
        long expectedRevision,
        ExpenseRecord replacement,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(replacement);

        if (!string.Equals(replacement.TransactionId, transactionId, StringComparison.Ordinal) ||
            !string.Equals(replacement.UserId, userId, StringComparison.Ordinal) ||
            replacement.Revision != expectedRevision + 1)
        {
            throw new ArgumentException(
                "The replacement Expense record identity or revision is invalid.",
                nameof(replacement));
        }

        if (!records.TryGetValue(transactionId, out var current) ||
            !string.Equals(current.UserId, userId, StringComparison.Ordinal))
        {
            return Task.FromResult(new ExpenseRecordMutationResult(null, Replaced: false));
        }

        if (current.Revision != expectedRevision)
        {
            return Task.FromResult(new ExpenseRecordMutationResult(current, Replaced: false));
        }

        var replaced = records.TryUpdate(transactionId, replacement, current);
        if (replaced)
        {
            return Task.FromResult(new ExpenseRecordMutationResult(replacement, Replaced: true));
        }

        records.TryGetValue(transactionId, out var latest);
        return Task.FromResult(
            new ExpenseRecordMutationResult(
                latest is not null &&
                string.Equals(latest.UserId, userId, StringComparison.Ordinal)
                    ? latest
                    : null,
                Replaced: false));
    }
}
