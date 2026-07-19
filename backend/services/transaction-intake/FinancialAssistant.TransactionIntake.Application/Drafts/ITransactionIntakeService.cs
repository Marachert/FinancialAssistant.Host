using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Application.Drafts;

public interface ITransactionIntakeService
{
    Task<TransactionIntakeResult> CreateDraftAsync(
        string userId,
        string idempotencyKey,
        TransactionIntakeRequest request,
        CancellationToken cancellationToken);
}

public sealed record TransactionIntakeResult(TransactionDraftResponse Draft, bool Replayed);
