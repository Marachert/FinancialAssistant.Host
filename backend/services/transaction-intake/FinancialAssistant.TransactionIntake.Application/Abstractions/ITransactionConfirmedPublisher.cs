using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Application.Abstractions;

public interface ITransactionConfirmedPublisher
{
    Task PublishAsync(
        TransactionConfirmedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}
