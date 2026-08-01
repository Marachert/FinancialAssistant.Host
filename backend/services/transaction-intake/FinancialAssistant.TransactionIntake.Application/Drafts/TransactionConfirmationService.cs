using System.Collections.Concurrent;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Application.Drafts;

public sealed class TransactionConfirmationService : ITransactionConfirmationService
{
    private readonly ConcurrentDictionary<string, ConfirmationGate> confirmationGates =
        new(StringComparer.Ordinal);

    private readonly ITransactionDraftStore draftStore;
    private readonly ITransactionConfirmationStore confirmationStore;
    private readonly ITransactionConfirmedPublisher publisher;
    private readonly ITransactionIntakeClock clock;
    private readonly ITransactionConfirmationIdGenerator idGenerator;

    public TransactionConfirmationService(
        ITransactionDraftStore draftStore,
        ITransactionConfirmationStore confirmationStore,
        ITransactionConfirmedPublisher publisher,
        ITransactionIntakeClock clock,
        ITransactionConfirmationIdGenerator idGenerator)
    {
        this.draftStore = draftStore;
        this.confirmationStore = confirmationStore;
        this.publisher = publisher;
        this.clock = clock;
        this.idGenerator = idGenerator;
    }

    public async Task<TransactionConfirmationResult?> ConfirmAsync(
        string userId,
        string draftId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeRequired(userId, nameof(userId));
        var normalizedDraftId = NormalizeRequired(draftId, nameof(draftId));
        var gateKey = $"{normalizedUserId.Length}:{normalizedUserId}{normalizedDraftId}";
        var gate = AcquireGate(gateKey);
        try
        {
            await gate.Semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ConfirmLockedAsync(
                    normalizedUserId,
                    normalizedDraftId,
                    correlationId,
                    cancellationToken);
            }
            finally
            {
                gate.Semaphore.Release();
            }
        }
        finally
        {
            ReleaseGate(gateKey, gate);
        }
    }

    private async Task<TransactionConfirmationResult?> ConfirmLockedAsync(
        string normalizedUserId,
        string normalizedDraftId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var existing = await confirmationStore.GetAsync(
                normalizedUserId,
                normalizedDraftId,
                cancellationToken);
            if (existing is not null)
            {
                await PublishIfPendingAsync(existing, cancellationToken);
                await MarkConfirmedAsync(normalizedUserId, normalizedDraftId);
                return new TransactionConfirmationResult(
                    ToResponse(existing.IntegrationEvent),
                    Replayed: true);
            }

            var draft = await draftStore.GetByIdAsync(
                normalizedUserId,
                normalizedDraftId,
                cancellationToken);
            if (draft is null)
            {
                return null;
            }

            EnsureConfirmable(draft);
            if (draft.Status == TransactionDraftStatuses.Draft)
            {
                var claim = await draftStore.ReplaceAsync(
                    normalizedUserId,
                    normalizedDraftId,
                    draft.Revision,
                    draft with
                    {
                        Status = TransactionDraftStatuses.Confirming,
                        Revision = draft.Revision + 1
                    },
                    cancellationToken);
                if (!claim.Replaced)
                {
                    continue;
                }

                draft = claim.Draft!;
            }
            else if (draft.Status != TransactionDraftStatuses.Confirming)
            {
                throw new DraftNotConfirmableException();
            }

            var confirmedAtUtc = clock.UtcNow.ToUniversalTime();
            var integrationEvent = new TransactionConfirmedIntegrationEvent(
                idGenerator.CreateEventId(),
                idGenerator.CreateTransactionId(),
                normalizedUserId,
                normalizedDraftId,
                draft.Type,
                draft.Amount!.Value,
                draft.Currency!,
                draft.CategoryId!,
                draft.Merchant,
                draft.Date!.Value,
                confirmedAtUtc,
                NormalizeCorrelationId(correlationId, normalizedDraftId));
            var stored = await confirmationStore.StoreIfMissingAsync(
                integrationEvent,
                cancellationToken);

            await PublishIfPendingAsync(stored.Stored, cancellationToken);
            await MarkConfirmedAsync(normalizedUserId, normalizedDraftId);

            return new TransactionConfirmationResult(
                ToResponse(stored.Stored.IntegrationEvent),
                Replayed: !stored.Created);
        }
    }

    private async Task PublishIfPendingAsync(
        StoredTransactionConfirmation stored,
        CancellationToken cancellationToken)
    {
        if (stored.Published)
        {
            return;
        }

        await publisher.PublishAsync(stored.IntegrationEvent, cancellationToken);
        await confirmationStore.MarkPublishedAsync(
            stored.IntegrationEvent.UserId,
            stored.IntegrationEvent.DraftId,
            stored.IntegrationEvent.EventId,
            CancellationToken.None);
    }

    private async Task MarkConfirmedAsync(string userId, string draftId)
    {
        while (true)
        {
            var draft = await draftStore.GetByIdAsync(userId, draftId, CancellationToken.None);
            if (draft is null ||
                draft.Status == TransactionDraftStatuses.Confirmed)
            {
                return;
            }

            if (draft.Status != TransactionDraftStatuses.Confirming)
            {
                return;
            }

            var result = await draftStore.ReplaceAsync(
                userId,
                draftId,
                draft.Revision,
                draft with
                {
                    Status = TransactionDraftStatuses.Confirmed,
                    Revision = draft.Revision + 1
                },
                CancellationToken.None);
            if (result.Replaced)
            {
                return;
            }
        }
    }

    private ConfirmationGate AcquireGate(string key)
    {
        while (true)
        {
            var gate = confirmationGates.GetOrAdd(key, _ => new ConfirmationGate());
            if (gate.TryAddReference())
            {
                return gate;
            }
        }
    }

    private void ReleaseGate(string key, ConfirmationGate gate)
    {
        if (!gate.ReleaseReference())
        {
            return;
        }

        confirmationGates.TryRemove(key, out _);
        gate.Dispose();
    }

    private static void EnsureConfirmable(TransactionDraft draft)
    {
        if (draft.Status is not TransactionDraftStatuses.Draft and
            not TransactionDraftStatuses.Confirming ||
            draft.Type is not TransactionTypes.Income and not TransactionTypes.Expense ||
            draft.Amount is null or <= 0 ||
            string.IsNullOrWhiteSpace(draft.Currency) ||
            string.IsNullOrWhiteSpace(draft.CategoryId) ||
            draft.Date is null ||
            draft.RequiresReview ||
            draft.Ambiguities.Count > 0)
        {
            throw new DraftNotConfirmableException();
        }
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentException("Value cannot exceed 200 characters.", parameterName);
        }

        return normalized;
    }

    private static string NormalizeCorrelationId(string? value, string draftId)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"confirm-{draftId}";
        }

        var normalized = value.Trim();
        return normalized.Length <= 128 ? normalized : normalized[..128];
    }

    private static ConfirmedTransactionResponse ToResponse(
        TransactionConfirmedIntegrationEvent integrationEvent) =>
        new(
            integrationEvent.TransactionId,
            integrationEvent.DraftId,
            TransactionDraftStatuses.Confirmed,
            integrationEvent.TransactionType,
            integrationEvent.Amount,
            integrationEvent.Currency,
            integrationEvent.CategoryId,
            integrationEvent.Merchant,
            integrationEvent.Date,
            integrationEvent.ConfirmedAtUtc);

    private sealed class ConfirmationGate : IDisposable
    {
        private readonly object sync = new();
        private int referenceCount;
        private bool removed;

        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public bool TryAddReference()
        {
            lock (sync)
            {
                if (removed)
                {
                    return false;
                }

                referenceCount++;
                return true;
            }
        }

        public bool ReleaseReference()
        {
            lock (sync)
            {
                referenceCount--;
                if (referenceCount != 0)
                {
                    return false;
                }

                removed = true;
                return true;
            }
        }

        public void Dispose() => Semaphore.Dispose();
    }
}
