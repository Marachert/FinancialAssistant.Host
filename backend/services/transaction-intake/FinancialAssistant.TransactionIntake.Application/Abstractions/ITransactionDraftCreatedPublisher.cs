using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Application.Abstractions;

public interface ITransactionDraftCreatedPublisher
{
    Task PublishAsync(
        TransactionDraftCreatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}
