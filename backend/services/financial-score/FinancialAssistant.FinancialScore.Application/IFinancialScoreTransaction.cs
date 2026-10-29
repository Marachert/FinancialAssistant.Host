using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.FinancialScore.Application;

public interface IFinancialScoreTransaction
{
    Task<T> ExecuteAsync<T>(IntegrationEventEnvelope<FinancialRecordChangedV1> source,
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    Task<T> ExecuteOwnerAsync<T>(string owner, Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);
}
