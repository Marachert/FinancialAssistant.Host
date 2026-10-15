using System.Text.Json.Serialization;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Domain.Drafts;
using FinancialAssistant.TransactionIntake.Infrastructure.Events;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Storage;

public sealed class PostgresTransactionConfirmationStore(IntakePersistenceContext context) : ITransactionConfirmationStore
{
    public Task<StoredTransactionConfirmation?> GetAsync(string userId, string draftId, CancellationToken token)
    {
        IntakeRecordCodec.Identifier(draftId);
        return context.WithRecordsAsync(userId, async (records, _) =>
        {
            var durable = await records.GetAsync("confirmations", userId, draftId);
            if (durable is null) { return null; }
            var value = Decode(durable, userId, draftId);
            return new StoredTransactionConfirmation(value.Event, value.Enqueued);
        }, token);
    }

    public Task<TransactionConfirmationStoreResult> StoreIfMissingAsync(TransactionConfirmedIntegrationEvent value, CancellationToken token)
    {
        IntakeRecordCodec.Confirmation(value);
        return context.WithRecordsAsync(value.UserId, async (records, _) =>
        {
            var existing = await records.GetAsync("confirmations", value.UserId, value.DraftId);
            if (existing is not null)
            {
                var state = Decode(existing, value.UserId, value.DraftId);
                if (state.Event != value) { throw new StorageConflictException(); }
                return new TransactionConfirmationStoreResult(new StoredTransactionConfirmation(state.Event, state.Enqueued), false);
            }
            var durable = await records.GetAsync("drafts", value.UserId, value.DraftId) ?? throw new StorageUnavailableException();
            var draft = IntakeRecordCodec.Decode(durable, value.UserId, value.DraftId);
            if (draft.Status != TransactionDraftStatuses.Confirming || draft.Type != value.TransactionType || draft.Amount != value.Amount
                || draft.Currency != value.Currency || draft.CategoryId != value.CategoryId || draft.Merchant != value.Merchant
                || draft.Date != value.Date || value.ConfirmedAtUtc < draft.CreatedAtUtc)
            { throw new StorageConflictException(); }
            if (await records.GetAsync("confirmation_ids", "intake", value.TransactionId) is not null)
            { throw new StorageConflictException(); }
            await records.InsertAsync("confirmation_ids", "intake", value.TransactionId,
                OwnedRecordCodec.Encode(1, new ConfirmationIdentity(value.UserId, value.DraftId, value.EventId)));
            await records.InsertAsync("confirmations", value.UserId, value.DraftId, OwnedRecordCodec.Encode(1, new State(value, false)));
            return new TransactionConfirmationStoreResult(new StoredTransactionConfirmation(value, false), true);
        }, token);
    }

    public async Task MarkPublishedAsync(string userId, string draftId, string eventId, CancellationToken token)
    {
        await context.WithRecordsAsync(userId, async (records, _) =>
        {
            var existing = await records.GetAsync("confirmations", userId, draftId) ?? throw new StorageUnavailableException();
            var state = Decode(existing, userId, draftId);
            if (state.Event.EventId != eventId) { throw new StorageUnavailableException(); }
            if (!state.Enqueued)
            {
                await PostgresIntakeOutbox.VerifyQueuedAsync(records, new IntakeOutboxMessage("confirmed", null, state.Event, null));
                await records.ReplaceAsync("confirmations", userId, draftId, existing.Revision, OwnedRecordCodec.Encode(1, state with { Enqueued = true }));
            }
            return true;
        }, token);
    }

    private static State Decode(DurableRecord durable, string owner, string draftId)
    {
        try
        {
            var state = OwnedRecordCodec.Decode<State>(1, durable);
            IntakeRecordCodec.Confirmation(state.Event);
            if (state.Event.UserId != owner || state.Event.DraftId != draftId || durable.Revision != (state.Enqueued ? 1 : 0))
            { throw new StorageUnavailableException(); }
            return state;
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    private sealed record State([property: JsonRequired] TransactionConfirmedIntegrationEvent Event, [property: JsonRequired] bool Enqueued);
    private sealed record ConfirmationIdentity([property: JsonRequired] string UserId, [property: JsonRequired] string DraftId,
        [property: JsonRequired] string EventId);
}
