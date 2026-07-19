using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Application.Abstractions;

public interface ITransactionDraftStore
{
    Task<StoredTransactionDraft?> GetAsync(
        string userId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<TransactionDraftStoreResult> StoreIfMissingAsync(
        string userId,
        string idempotencyKey,
        string inputFingerprint,
        TransactionDraft draft,
        CancellationToken cancellationToken);
}

public sealed record StoredTransactionDraft(string InputFingerprint, TransactionDraft Draft);

public sealed record TransactionDraftStoreResult(StoredTransactionDraft Stored, bool Created);
