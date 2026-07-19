namespace FinancialAssistant.TransactionIntake.Application.Abstractions;

public interface ITransactionConfirmationIdGenerator
{
    string CreateTransactionId();

    string CreateEventId();
}
