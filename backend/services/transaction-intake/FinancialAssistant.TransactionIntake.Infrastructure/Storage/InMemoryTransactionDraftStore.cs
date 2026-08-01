using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Storage;

public sealed class InMemoryTransactionDraftStore : ITransactionDraftStore
{
    private readonly object sync = new();
    private readonly Dictionary<string, StoredTransactionDraft> drafts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TransactionDraft> draftsById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> draftStorageKeysById = new(StringComparer.Ordinal);

    public Task<StoredTransactionDraft?> GetAsync(
        string userId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            drafts.TryGetValue(CreateKey(userId, idempotencyKey), out var stored);
            return Task.FromResult(stored);
        }
    }

    public Task<TransactionDraft?> GetByIdAsync(
        string userId,
        string draftId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            draftsById.TryGetValue(CreateKey(userId, draftId), out var draft);
            return Task.FromResult(draft);
        }
    }

    public Task<TransactionDraftStoreResult> StoreIfMissingAsync(
        string userId,
        string idempotencyKey,
        string inputFingerprint,
        TransactionDraft draft,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (sync)
        {
            var storageKey = CreateKey(userId, idempotencyKey);
            if (drafts.TryGetValue(storageKey, out var existing))
            {
                return Task.FromResult(new TransactionDraftStoreResult(existing, Created: false));
            }

            var draftKey = CreateKey(userId, draft.Id);
            if (draftsById.ContainsKey(draftKey))
            {
                throw new InvalidOperationException("A transaction draft with the generated identifier already exists.");
            }

            var stored = new StoredTransactionDraft(inputFingerprint, draft);
            drafts.Add(storageKey, stored);
            draftsById.Add(draftKey, draft);
            draftStorageKeysById.Add(draftKey, storageKey);
            return Task.FromResult(new TransactionDraftStoreResult(stored, Created: true));
        }
    }

    public Task<TransactionDraftMutationResult> ReplaceAsync(
        string userId,
        string draftId,
        long expectedRevision,
        TransactionDraft replacement,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(replacement);

        if (!string.Equals(replacement.UserId, userId, StringComparison.Ordinal) ||
            !string.Equals(replacement.Id, draftId, StringComparison.Ordinal) ||
            replacement.Revision != expectedRevision + 1)
        {
            throw new ArgumentException("The replacement draft identity or revision is invalid.", nameof(replacement));
        }

        lock (sync)
        {
            var draftKey = CreateKey(userId, draftId);
            if (!draftsById.TryGetValue(draftKey, out var current))
            {
                return Task.FromResult(new TransactionDraftMutationResult(null, Replaced: false));
            }

            if (current.Revision != expectedRevision)
            {
                return Task.FromResult(new TransactionDraftMutationResult(current, Replaced: false));
            }

            draftsById[draftKey] = replacement;
            if (draftStorageKeysById.TryGetValue(draftKey, out var storageKey) &&
                drafts.TryGetValue(storageKey, out var stored))
            {
                drafts[storageKey] = stored with { Draft = replacement };
            }

            return Task.FromResult(new TransactionDraftMutationResult(replacement, Replaced: true));
        }
    }

    private static string CreateKey(string userId, string value) =>
        $"{userId.Length}:{userId}{value}";
}
