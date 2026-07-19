using FinancialAssistant.TransactionIntake.Application.Abstractions;

namespace FinancialAssistant.TransactionIntake.Application;

public sealed class GuidTransactionDraftIdGenerator : ITransactionDraftIdGenerator
{
    public string Create() => $"draft_{Guid.NewGuid():N}";
}
