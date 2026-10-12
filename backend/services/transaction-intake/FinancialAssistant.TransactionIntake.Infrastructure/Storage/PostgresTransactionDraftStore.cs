using System.Text.Json.Serialization;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Application.Drafts;
using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Storage;

public sealed class PostgresTransactionDraftStore(IntakePersistenceContext context) : ITransactionDraftStore
{
    public Task<StoredTransactionDraft?> GetAsync(string userId, string idempotencyKey, CancellationToken cancellationToken)
    {
        IntakeRecordCodec.Identifier(idempotencyKey);
        return context.WithRecordsAsync(userId, async (records, _) =>
        {
            var index = await records.GetAsync("draft_keys", userId, idempotencyKey);
            if (index is null) { return null; }
            var reference = Reference(index);
            var durable = await records.GetAsync("drafts", userId, reference.DraftId) ?? throw new StorageUnavailableException();
            var draft = IntakeRecordCodec.Decode(durable, userId, reference.DraftId);
            if (draft.InputFingerprint != reference.InputFingerprint) { throw new StorageUnavailableException(); }
            return new StoredTransactionDraft(reference.InputFingerprint, draft);
        }, cancellationToken);
    }

    public Task<TransactionDraft?> GetByIdAsync(string userId, string draftId, CancellationToken cancellationToken)
    {
        IntakeRecordCodec.Identifier(draftId);
        return context.WithRecordsAsync(userId, async (records, _) =>
        {
            var durable = await records.GetAsync("drafts", userId, draftId);
            return durable is null ? null : IntakeRecordCodec.Decode(durable, userId, draftId);
        }, cancellationToken);
    }

    public Task<TransactionDraftStoreResult> StoreIfMissingAsync(string userId, string idempotencyKey, string inputFingerprint,
        TransactionDraft draft, CancellationToken cancellationToken)
    {
        IntakeRecordCodec.Identifier(idempotencyKey);
        IntakeRecordCodec.InputFingerprint(inputFingerprint);
        IntakeRecordCodec.Validate(draft);
        if (draft.UserId != userId || draft.InputFingerprint != inputFingerprint || draft.Revision != 0 || draft.Status != TransactionDraftStatuses.Draft)
        { throw new ArgumentException("Invalid initial Intake draft identity or state."); }
        return context.WithRecordsAsync(userId, async (records, _) =>
        {
            var existing = await records.GetAsync("draft_keys", userId, idempotencyKey);
            if (existing is not null)
            {
                var reference = Reference(existing);
                if (reference.InputFingerprint != inputFingerprint) { throw new IdempotencyConflictException(); }
                var durable = await records.GetAsync("drafts", userId, reference.DraftId) ?? throw new StorageUnavailableException();
                var current = IntakeRecordCodec.Decode(durable, userId, reference.DraftId);
                if (current.InputFingerprint != reference.InputFingerprint) { throw new StorageUnavailableException(); }
                return new TransactionDraftStoreResult(new StoredTransactionDraft(reference.InputFingerprint, current), false);
            }
            if (await records.GetAsync("draft_owners", "intake", draft.Id) is not null)
            { throw new StorageConflictException(); }
            await records.InsertAsync("draft_owners", "intake", draft.Id, OwnedRecordCodec.Encode(1, new OwnerMarker(userId)));
            await records.InsertAsync("drafts", userId, draft.Id, IntakeRecordCodec.Encode(draft));
            await records.InsertAsync("draft_keys", userId, idempotencyKey, OwnedRecordCodec.Encode(1, new DraftReference(draft.Id, inputFingerprint)));
            return new TransactionDraftStoreResult(new StoredTransactionDraft(inputFingerprint, draft), true);
        }, cancellationToken);
    }

    public Task<TransactionDraftMutationResult> ReplaceAsync(string userId, string draftId, long expectedRevision,
        TransactionDraft replacement, CancellationToken cancellationToken)
    {
        IntakeRecordCodec.Validate(replacement);
        if (replacement.UserId != userId || replacement.Id != draftId || expectedRevision < 0 || expectedRevision >= long.MaxValue - 1
            || replacement.Revision != expectedRevision + 1) { throw new ArgumentException("Invalid Intake replacement identity or revision."); }
        return context.WithRecordsAsync(userId, async (records, _) =>
        {
            var durable = await records.GetAsync("drafts", userId, draftId);
            if (durable is null) { return new TransactionDraftMutationResult(null, false); }
            var current = IntakeRecordCodec.Decode(durable, userId, draftId);
            if (current.Revision != expectedRevision) { return new TransactionDraftMutationResult(current, false); }
            if (current.InputFingerprint != replacement.InputFingerprint || current.CreatedAtUtc != replacement.CreatedAtUtc
                || current.InputSource != replacement.InputSource || current.Suggestion.Source != replacement.Suggestion.Source
                || current.Suggestion.SourceReferenceId != replacement.Suggestion.SourceReferenceId)
            { throw new ArgumentException("Intake source identity is immutable."); }
            var allowed = current.Status == TransactionDraftStatuses.Draft && replacement.Status is
                TransactionDraftStatuses.Draft or TransactionDraftStatuses.Confirming or TransactionDraftStatuses.Rejected
                || current.Status == TransactionDraftStatuses.Confirming && replacement.Status == TransactionDraftStatuses.Confirmed;
            if (!allowed) { throw new DraftMutationConflictException(); }
            await records.ReplaceAsync("drafts", userId, draftId, durable.Revision, IntakeRecordCodec.Encode(replacement));
            return new TransactionDraftMutationResult(replacement, true);
        }, cancellationToken);
    }

    private static DraftReference Reference(DurableRecord record)
    {
        try
        {
            var value = OwnedRecordCodec.Decode<DraftReference>(1, record);
            IntakeRecordCodec.Identifier(value.DraftId); IntakeRecordCodec.InputFingerprint(value.InputFingerprint);
            return value;
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    private sealed record DraftReference([property: JsonRequired] string DraftId, [property: JsonRequired] string InputFingerprint);
    private sealed record OwnerMarker([property: JsonRequired] string UserId);
}
