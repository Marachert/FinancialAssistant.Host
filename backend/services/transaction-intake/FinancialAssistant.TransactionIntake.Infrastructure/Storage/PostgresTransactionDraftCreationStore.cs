using System.Text.Json.Serialization;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Infrastructure.Events;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Storage;

public sealed class PostgresTransactionDraftCreationStore(IntakePersistenceContext context,
    IntakeRetentionSettings retention, ITransactionIntakeClock clock) : ITransactionDraftCreationStore
{
    public Task<StoredTransactionDraftCreation?> GetByReferenceAsync(string userId, string sourcePayloadReferenceId, CancellationToken token)
    {
        IntakeRecordCodec.Identifier(sourcePayloadReferenceId);
        return context.WithRecordsAsync(userId, async (records, _) =>
        {
            var source = await records.GetAsync("source_payloads", "intake", sourcePayloadReferenceId);
            if (source is null) { return null; }
            var payload = DecodeSource(source);
            if (payload.Event.UserId != userId || payload.Event.SourcePayloadReferenceId != sourcePayloadReferenceId || Expired(payload))
            { return null; }
            var durable = await records.GetAsync("draft_creations", userId, payload.Event.DraftId) ?? throw new StorageUnavailableException();
            var state = DecodeCreation(durable, userId, payload.Event.DraftId);
            if (state.Event != payload.Event || state.InputFingerprint != IntakeRecordCodec.SourceFingerprint(payload.NormalizedInput))
            { throw new StorageUnavailableException(); }
            return new StoredTransactionDraftCreation(state.Event, payload.NormalizedInput, state.Enqueued);
        }, token);
    }

    public Task<TransactionDraftCreationStoreResult> StoreIfMissingAsync(TransactionDraftCreatedIntegrationEvent value,
        string normalizedInput, CancellationToken token)
    {
        IntakeRecordCodec.Creation(value);
        var fingerprint = IntakeRecordCodec.SourceFingerprint(normalizedInput);
        retention.Validate();
        return context.WithRecordsAsync(value.UserId, async (records, _) =>
        {
            var currentDraft = await records.GetAsync("drafts", value.UserId, value.DraftId) ?? throw new StorageUnavailableException();
            var draft = IntakeRecordCodec.Decode(currentDraft, value.UserId, value.DraftId);
            if (draft.CreatedAtUtc != value.OccurredAtUtc || draft.InputFingerprint != fingerprint)
            { throw new StorageConflictException(); }
            var existing = await records.GetAsync("draft_creations", value.UserId, value.DraftId);
            if (existing is not null)
            {
                var state = DecodeCreation(existing, value.UserId, value.DraftId);
                if (state.Event != value || state.InputFingerprint != fingerprint) { throw new StorageConflictException(); }
                // Replay preserves the original expiry and cannot restore deleted source text.
                var source = await records.GetAsync("source_payloads", "intake", value.SourcePayloadReferenceId);
                var payload = source is null ? null : DecodeSource(source);
                if (payload is not null && (payload.Event != value || IntakeRecordCodec.SourceFingerprint(payload.NormalizedInput) != fingerprint))
                { throw new StorageUnavailableException(); }
                return new TransactionDraftCreationStoreResult(new StoredTransactionDraftCreation(state.Event,
                    payload is null || Expired(payload) ? "" : payload.NormalizedInput, state.Enqueued), false);
            }
            if (await records.GetAsync("source_references", "intake", value.SourcePayloadReferenceId) is not null)
            { throw new StorageConflictException(); }
            await records.InsertAsync("source_references", "intake", value.SourcePayloadReferenceId,
                OwnedRecordCodec.Encode(1, new SourceReference(value.UserId, value.DraftId)));
            await records.InsertAsync("draft_creations", value.UserId, value.DraftId,
                OwnedRecordCodec.Encode(1, new CreationState(value, fingerprint, false)));
            var payloadState = new SourcePayload(value, normalizedInput, value.OccurredAtUtc.AddHours(retention.SourcePayloadRetentionHours));
            if (!Expired(payloadState))
            { await records.InsertAsync("source_payloads", "intake", value.SourcePayloadReferenceId, OwnedRecordCodec.Encode(1, payloadState)); }
            return new TransactionDraftCreationStoreResult(new StoredTransactionDraftCreation(value,
                Expired(payloadState) ? "" : normalizedInput, false), true);
        }, token);
    }

    public async Task MarkPublishedAsync(string userId, string draftId, string eventId, CancellationToken token)
    {
        await context.WithRecordsAsync(userId, async (records, _) =>
        {
            var existing = await records.GetAsync("draft_creations", userId, draftId) ?? throw new StorageUnavailableException();
            var state = DecodeCreation(existing, userId, draftId);
            if (state.Event.EventId != eventId) { throw new StorageUnavailableException(); }
            if (!state.Enqueued)
            {
                await PostgresIntakeOutbox.VerifyQueuedAsync(records, new IntakeOutboxMessage("draft-created", state.Event, null, null));
                await records.ReplaceAsync("draft_creations", userId, draftId, existing.Revision,
                    OwnedRecordCodec.Encode(1, state with { Enqueued = true }));
            }
            return true;
        }, token);
    }

    public Task<int> CleanupExpiredBatchAsync(CancellationToken token = default) =>
        context.WithRecordsAsync("source-maintenance", async (records, _) =>
        {
            var scan = await records.GetAsync("payload_scan", "intake", "retention");
            var after = scan is null ? null : OwnedRecordCodec.Decode<ScanCursor>(1, scan).After;
            var page = await records.ReadPageAsync("source_payloads", "intake", after, 25);
            var removed = 0;
            foreach (var item in page)
            {
                var payload = DecodeSource(new DurableRecord(item.Revision, item.Payload));
                if (Expired(payload)) { await records.DeletePageItemAsync("source_payloads", "intake", item.Cursor, item.Revision); removed++; }
            }
            var cursor = new ScanCursor(page.Count < 25 ? null : page[^1].Cursor);
            if (scan is null) { await records.InsertAsync("payload_scan", "intake", "retention", OwnedRecordCodec.Encode(1, cursor)); }
            else { await records.ReplaceAsync("payload_scan", "intake", "retention", scan.Revision, OwnedRecordCodec.Encode(1, cursor)); }
            return removed;
        }, token);

    private bool Expired(SourcePayload value)
    {
        retention.Validate();
        var configured = TimeSpan.FromHours(retention.SourcePayloadRetentionHours);
        var effectiveExpiry = value.ExpiresAtUtc - value.Event.OccurredAtUtc > configured
            ? value.Event.OccurredAtUtc.Add(configured) : value.ExpiresAtUtc;
        return clock.UtcNow >= effectiveExpiry;
    }

    private static CreationState DecodeCreation(DurableRecord durable, string owner, string draftId)
    {
        try
        {
            var value = OwnedRecordCodec.Decode<CreationState>(1, durable);
            IntakeRecordCodec.Creation(value.Event); IntakeRecordCodec.InputFingerprint(value.InputFingerprint);
            if (value.Event.UserId != owner || value.Event.DraftId != draftId || durable.Revision != (value.Enqueued ? 1 : 0))
            { throw new StorageUnavailableException(); }
            return value;
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    private static SourcePayload DecodeSource(DurableRecord durable)
    {
        try
        {
            var value = OwnedRecordCodec.Decode<SourcePayload>(1, durable);
            IntakeRecordCodec.Creation(value.Event);
            _ = IntakeRecordCodec.SourceFingerprint(value.NormalizedInput);
            var lifetime = value.ExpiresAtUtc - value.Event.OccurredAtUtc;
            if (value.ExpiresAtUtc.Offset != TimeSpan.Zero || lifetime.TotalHours is < 1 or > 24
                || lifetime.Ticks % TimeSpan.TicksPerHour != 0 || durable.Revision != 0)
            { throw new StorageUnavailableException(); }
            return value;
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    private sealed record CreationState([property: JsonRequired] TransactionDraftCreatedIntegrationEvent Event,
        [property: JsonRequired] string InputFingerprint, [property: JsonRequired] bool Enqueued);
    private sealed record SourcePayload([property: JsonRequired] TransactionDraftCreatedIntegrationEvent Event,
        [property: JsonRequired] string NormalizedInput, [property: JsonRequired] DateTimeOffset ExpiresAtUtc);
    private sealed record SourceReference([property: JsonRequired] string UserId, [property: JsonRequired] string DraftId);
    private sealed record ScanCursor([property: JsonRequired] string? After);
}
