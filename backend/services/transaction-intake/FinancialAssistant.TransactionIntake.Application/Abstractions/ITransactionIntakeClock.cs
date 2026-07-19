namespace FinancialAssistant.TransactionIntake.Application.Abstractions;

public interface ITransactionIntakeClock
{
    DateTimeOffset UtcNow { get; }
}
