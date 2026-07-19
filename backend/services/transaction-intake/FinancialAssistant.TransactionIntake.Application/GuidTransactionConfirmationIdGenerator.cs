using FinancialAssistant.TransactionIntake.Application.Abstractions;

namespace FinancialAssistant.TransactionIntake.Application;

public sealed class GuidTransactionConfirmationIdGenerator : ITransactionConfirmationIdGenerator
{
    public string CreateTransactionId() => $"txn_{Guid.NewGuid():N}";

    public string CreateEventId() => $"event_{Guid.NewGuid():N}";
}
