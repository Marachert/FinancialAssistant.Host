using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.Audit.Application;

public interface IAuditEventTransaction
{
    Task<string> ExecuteAsync(IntegrationEventEnvelope<AuditEventV1> source,
        Func<CancellationToken, Task<string>> operation, CancellationToken cancellationToken);
}
