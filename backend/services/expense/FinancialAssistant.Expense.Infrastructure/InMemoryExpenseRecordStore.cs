using System.Collections.Concurrent;
using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Domain;

namespace FinancialAssistant.Expense.Infrastructure;

public sealed class InMemoryExpenseRecordStore : IExpenseRecordStore
{
    private readonly ConcurrentDictionary<string, ExpenseRecord> records = new(StringComparer.Ordinal);

    public IReadOnlyCollection<ExpenseRecord> Records => records.Values.ToArray();

    public Task<ExpenseRecord> StoreIfMissingAsync(
        ExpenseRecord record,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(records.GetOrAdd(record.TransactionId, record));
    }
}
