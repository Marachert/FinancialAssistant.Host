using System.Collections.Concurrent;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Application.Drafts;

public sealed class TransactionConfirmationService : ITransactionConfirmationService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> confirmationGates = new(StringComparer.Ordinal);
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
        var gate = confirmationGates.GetOrAdd(
            $"{normalizedUserId.Length}:{normalizedUserId}{normalizedDraftId}",
            _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
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
            gate.Release();
        }
    }

    private async Task<TransactionConfirmationResult?> ConfirmLockedAsync(
        string normalizedUserId,
        string normalizedDraftId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var existing = await confirmationStore.GetAsync(
            normalizedUserId,
            normalizedDraftId,
            cancellationToken);
        if (existing is not null)
        {
            await PublishIfPendingAsync(existing, cancellationToken);
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

        return new TransactionConfirmationResult(
            ToResponse(stored.Stored.IntegrationEvent),
            Replayed: !stored.Created);
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

    private static void EnsureConfirmable(TransactionDraft draft)
    {
        if (draft.Type is not TransactionTypes.Income and not TransactionTypes.Expense ||
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
            "confirmed",
            integrationEvent.TransactionType,
            integrationEvent.Amount,
            integrationEvent.Currency,
            integrationEvent.CategoryId,
            integrationEvent.Merchant,
            integrationEvent.Date,
            integrationEvent.ConfirmedAtUtc);
}
