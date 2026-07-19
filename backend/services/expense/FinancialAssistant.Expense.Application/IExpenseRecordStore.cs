using FinancialAssistant.Expense.Domain;

namespace FinancialAssistant.Expense.Application;

public interface IExpenseRecordStore
{
    Task<ExpenseRecord> StoreIfMissingAsync(ExpenseRecord record, CancellationToken cancellationToken);
}
