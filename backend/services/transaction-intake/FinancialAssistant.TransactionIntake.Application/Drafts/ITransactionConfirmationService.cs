using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Application.Drafts;

public interface ITransactionConfirmationService
{
    Task<TransactionConfirmationResult?> ConfirmAsync(
        string userId,
        string draftId,
        string? correlationId,
        CancellationToken cancellationToken);
}

public sealed record TransactionConfirmationResult(
    ConfirmedTransactionResponse Transaction,
    bool Replayed);
