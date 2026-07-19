using FinancialAssistant.TransactionIntake.Application.Abstractions;

namespace FinancialAssistant.TransactionIntake.Application;

public sealed class SystemTransactionIntakeClock : ITransactionIntakeClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
