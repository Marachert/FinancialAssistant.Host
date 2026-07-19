using FinancialAssistant.Income.Domain;

namespace FinancialAssistant.Income.Application;

public interface IIncomeRecordStore
{
    Task<IncomeRecord> StoreIfMissingAsync(IncomeRecord record, CancellationToken cancellationToken);
}
