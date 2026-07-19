using System.Collections.Concurrent;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Storage;

public sealed class InMemoryTransactionConfirmationStore : ITransactionConfirmationStore
{
    private readonly ConcurrentDictionary<string, StoredTransactionConfirmation> confirmations =
        new(StringComparer.Ordinal);

    public Task<StoredTransactionConfirmation?> GetAsync(
        string userId,
        string draftId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        confirmations.TryGetValue(CreateKey(userId, draftId), out var stored);
        return Task.FromResult(stored);
    }

    public Task<TransactionConfirmationStoreResult> StoreIfMissingAsync(
        TransactionConfirmedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var candidate = new StoredTransactionConfirmation(integrationEvent, Published: false);
        var stored = confirmations.GetOrAdd(
            CreateKey(integrationEvent.UserId, integrationEvent.DraftId),
            candidate);
        return Task.FromResult(
            new TransactionConfirmationStoreResult(
                stored,
                Created: ReferenceEquals(stored, candidate)));
    }

    public Task MarkPublishedAsync(
        string userId,
        string draftId,
        string eventId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = CreateKey(userId, draftId);
        while (confirmations.TryGetValue(key, out var existing))
        {
            if (!string.Equals(existing.IntegrationEvent.EventId, eventId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Confirmation event identity does not match stored state.");
            }

            if (existing.Published || confirmations.TryUpdate(key, existing with { Published = true }, existing))
            {
                return Task.CompletedTask;
            }
        }

        throw new InvalidOperationException("Confirmation state was not found.");
    }

    private static string CreateKey(string userId, string draftId) =>
        $"{userId.Length}:{userId}{draftId}";
}
