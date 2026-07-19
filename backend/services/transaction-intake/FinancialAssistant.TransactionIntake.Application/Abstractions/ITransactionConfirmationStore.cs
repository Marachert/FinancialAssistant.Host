using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Application.Abstractions;

public interface ITransactionConfirmationStore
{
    Task<StoredTransactionConfirmation?> GetAsync(
        string userId,
        string draftId,
        CancellationToken cancellationToken);

    Task<TransactionConfirmationStoreResult> StoreIfMissingAsync(
        TransactionConfirmedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);

    Task MarkPublishedAsync(
        string userId,
        string draftId,
        string eventId,
        CancellationToken cancellationToken);
}

public sealed record StoredTransactionConfirmation(
    TransactionConfirmedIntegrationEvent IntegrationEvent,
    bool Published);

public sealed record TransactionConfirmationStoreResult(
    StoredTransactionConfirmation Stored,
    bool Created);
