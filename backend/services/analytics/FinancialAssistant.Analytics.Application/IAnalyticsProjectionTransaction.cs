using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.Analytics.Application;

public interface IAnalyticsProjectionTransaction
{
    Task ExecuteAsync(IntegrationEventEnvelope<FinancialRecordChangedV1> source,
        Func<CancellationToken, Task> apply, CancellationToken cancellationToken);
}
