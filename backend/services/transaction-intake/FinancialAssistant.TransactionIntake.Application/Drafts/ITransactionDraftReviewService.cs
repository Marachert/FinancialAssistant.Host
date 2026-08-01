using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Application.Drafts;

public interface ITransactionDraftReviewService
{
    Task<TransactionDraftResponse?> ReviewAsync(
        string userId,
        string draftId,
        CancellationToken cancellationToken);

    Task<TransactionDraftResponse?> UpdateAsync(
        string userId,
        string draftId,
        TransactionDraftUpdateRequest request,
        CancellationToken cancellationToken);

    Task<TransactionDraftResponse?> RejectAsync(
        string userId,
        string draftId,
        CancellationToken cancellationToken);
}
