using System.Collections.Concurrent;
using FinancialAssistant.Income.Application;
using FinancialAssistant.Income.Domain;

namespace FinancialAssistant.Income.Infrastructure;

public sealed class InMemoryIncomeRecordStore : IIncomeRecordStore
{
    private readonly ConcurrentDictionary<string, IncomeRecord> records =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<IncomeRecord> Records => records.Values.ToArray();

    public Task<IncomeRecord> StoreIfMissingAsync(
        IncomeRecord record,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(records.GetOrAdd(record.TransactionId, record));
    }

    public Task<bool> CreateAsync(
        IncomeRecord record,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(records.TryAdd(record.TransactionId, record));
    }

    public Task<IncomeRecord?> GetAsync(
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

    public Task<IReadOnlyList<IncomeRecord>> ListAsync(
        string userId,
        DateOnly from,
        DateOnly to,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<IncomeRecord> result = records.Values
            .Where(record =>
                string.Equals(record.UserId, userId, StringComparison.Ordinal) &&
                record.Date >= from &&
                record.Date <= to &&
                (includeArchived || record.Status == IncomeRecordStatuses.Active))
            .OrderByDescending(record => record.Date)
            .ThenBy(record => record.TransactionId, StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<IncomeRecordMutationResult> ReplaceAsync(
        string userId,
        string transactionId,
        long expectedRevision,
        IncomeRecord replacement,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(replacement);

        if (!string.Equals(replacement.TransactionId, transactionId, StringComparison.Ordinal) ||
            !string.Equals(replacement.UserId, userId, StringComparison.Ordinal) ||
            replacement.Revision != expectedRevision + 1)
        {
            throw new ArgumentException(
                "The replacement Income record identity or revision is invalid.",
                nameof(replacement));
        }

        if (!records.TryGetValue(transactionId, out var current) ||
            !string.Equals(current.UserId, userId, StringComparison.Ordinal))
        {
            return Task.FromResult(new IncomeRecordMutationResult(null, Replaced: false));
        }

        if (current.Revision != expectedRevision)
        {
            return Task.FromResult(new IncomeRecordMutationResult(current, Replaced: false));
        }

        var replaced = records.TryUpdate(transactionId, replacement, current);
        if (replaced)
        {
            return Task.FromResult(new IncomeRecordMutationResult(replacement, Replaced: true));
        }

        records.TryGetValue(transactionId, out var latest);
        return Task.FromResult(
            new IncomeRecordMutationResult(
                latest is not null &&
                string.Equals(latest.UserId, userId, StringComparison.Ordinal)
                    ? latest
                    : null,
                Replaced: false));
    }
}
