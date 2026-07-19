using System.Collections.Concurrent;
using FinancialAssistant.Income.Application;
using FinancialAssistant.Income.Domain;

namespace FinancialAssistant.Income.Infrastructure;

public sealed class InMemoryIncomeRecordStore : IIncomeRecordStore
{
    private readonly ConcurrentDictionary<string, IncomeRecord> records = new(StringComparer.Ordinal);

    public IReadOnlyCollection<IncomeRecord> Records => records.Values.ToArray();

    public Task<IncomeRecord> StoreIfMissingAsync(
        IncomeRecord record,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(records.GetOrAdd(record.TransactionId, record));
    }
}
