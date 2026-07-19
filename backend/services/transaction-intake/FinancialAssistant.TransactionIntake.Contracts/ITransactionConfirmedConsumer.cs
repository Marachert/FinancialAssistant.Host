namespace FinancialAssistant.TransactionIntake.Contracts;

public interface ITransactionConfirmedConsumer
{
    Task ConsumeAsync(
        TransactionConfirmedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}
