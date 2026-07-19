using System.Collections.Concurrent;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Storage;

public sealed class InMemoryTransactionDraftStore : ITransactionDraftStore
{
    private readonly ConcurrentDictionary<string, StoredTransactionDraft> drafts = new(StringComparer.Ordinal);

    public Task<StoredTransactionDraft?> GetAsync(
        string userId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        drafts.TryGetValue(CreateKey(userId, idempotencyKey), out var stored);
        return Task.FromResult(stored);
    }

    public Task<TransactionDraftStoreResult> StoreIfMissingAsync(
        string userId,
        string idempotencyKey,
        string inputFingerprint,
        TransactionDraft draft,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var candidate = new StoredTransactionDraft(inputFingerprint, draft);
        var stored = drafts.GetOrAdd(CreateKey(userId, idempotencyKey), candidate);
        return Task.FromResult(new TransactionDraftStoreResult(stored, ReferenceEquals(stored, candidate)));
    }

    private static string CreateKey(string userId, string idempotencyKey) =>
        $"{userId.Length}:{userId}{idempotencyKey}";
}
