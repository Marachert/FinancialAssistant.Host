using System.Collections.Concurrent;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Contracts;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Storage;

public sealed class InMemoryTransactionDraftCreationStore : ITransactionDraftCreationStore
{
    private readonly ConcurrentDictionary<string, StoredTransactionDraftCreation> creations =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, StoredTransactionDraftCreation> payloads =
        new(StringComparer.Ordinal);

    public Task<StoredTransactionDraftCreation?> GetByReferenceAsync(
        string userId,
        string sourcePayloadReferenceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        payloads.TryGetValue(CreateKey(userId, sourcePayloadReferenceId), out var stored);
        return Task.FromResult(stored);
    }

    public Task<TransactionDraftCreationStoreResult> StoreIfMissingAsync(
        TransactionDraftCreatedIntegrationEvent integrationEvent,
        string normalizedInput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(normalizedInput))
        {
            throw new ArgumentException("Normalized input is required.", nameof(normalizedInput));
        }

        var key = CreateKey(integrationEvent.UserId, integrationEvent.DraftId);
        var candidate = new StoredTransactionDraftCreation(
            integrationEvent,
            normalizedInput,
            Published: false);
        var stored = creations.GetOrAdd(key, candidate);
        if (stored.IntegrationEvent != integrationEvent ||
            !string.Equals(stored.NormalizedInput, normalizedInput, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Draft creation state conflicts with stored data.");
        }

        payloads.TryAdd(
            CreateKey(integrationEvent.UserId, integrationEvent.SourcePayloadReferenceId),
            stored);
        return Task.FromResult(
            new TransactionDraftCreationStoreResult(stored, ReferenceEquals(stored, candidate)));
    }

    public Task MarkPublishedAsync(
        string userId,
        string draftId,
        string eventId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = CreateKey(userId, draftId);
        while (creations.TryGetValue(key, out var stored))
        {
            if (!string.Equals(
                    stored.IntegrationEvent.EventId,
                    eventId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Draft creation event does not match stored data.");
            }

            if (stored.Published)
            {
                return Task.CompletedTask;
            }

            var published = stored with { Published = true };
            if (!creations.TryUpdate(key, published, stored))
            {
                continue;
            }

            payloads[CreateKey(
                userId,
                stored.IntegrationEvent.SourcePayloadReferenceId)] = published;
            return Task.CompletedTask;
        }

        throw new InvalidOperationException("Draft creation state was not found.");
    }

    private static string CreateKey(string userId, string value) =>
        $"{userId.Length}:{userId}{value}";
}
