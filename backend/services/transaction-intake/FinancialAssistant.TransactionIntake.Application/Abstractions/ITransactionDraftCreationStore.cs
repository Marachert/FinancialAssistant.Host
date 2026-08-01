using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Application.Abstractions;

public interface ITransactionDraftCreationStore
{
    Task<StoredTransactionDraftCreation?> GetByReferenceAsync(
        string userId,
        string sourcePayloadReferenceId,
        CancellationToken cancellationToken);

    Task<TransactionDraftCreationStoreResult> StoreIfMissingAsync(
        TransactionDraftCreatedIntegrationEvent integrationEvent,
        string normalizedInput,
        CancellationToken cancellationToken);

    Task MarkPublishedAsync(
        string userId,
        string draftId,
        string eventId,
        CancellationToken cancellationToken);
}

public sealed record StoredTransactionDraftCreation(
    TransactionDraftCreatedIntegrationEvent IntegrationEvent,
    string NormalizedInput,
    bool Published);

public sealed record TransactionDraftCreationStoreResult(
    StoredTransactionDraftCreation Stored,
    bool Created);
