using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Application.Drafts;

public sealed partial class TransactionIntakeService : ITransactionIntakeService
{
    public const int MaximumInputLength = 2000;

    private readonly ConcurrentDictionary<string, DraftCreatedEventGate> draftCreatedEventGates =
        new(StringComparer.Ordinal);

    private readonly ITransactionInputParser parser;
    private readonly ITransactionDraftStore store;
    private readonly ITransactionDraftCreationStore draftCreationStore;
    private readonly ITransactionDraftCreatedPublisher draftCreatedPublisher;
    private readonly ITransactionIntakeClock clock;
    private readonly ITransactionDraftIdGenerator idGenerator;
    private readonly TransactionDraftValidator validator;

    public TransactionIntakeService(
        ITransactionInputParser parser,
        ITransactionDraftStore store,
        ITransactionDraftCreationStore draftCreationStore,
        ITransactionDraftCreatedPublisher draftCreatedPublisher,
        ITransactionIntakeClock clock,
        ITransactionDraftIdGenerator idGenerator,
        TransactionDraftValidator validator)
    {
        this.parser = parser;
        this.store = store;
        this.draftCreationStore = draftCreationStore;
        this.draftCreatedPublisher = draftCreatedPublisher;
        this.clock = clock;
        this.idGenerator = idGenerator;
        this.validator = validator;
    }

    public async Task<TransactionIntakeResult> CreateDraftAsync(
        string userId,
        string idempotencyKey,
        TransactionIntakeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedUserId = NormalizeRequired(userId, nameof(userId), 200);
        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey);
        var normalizedInput = NormalizeRequired(request.Input, nameof(request), MaximumInputLength);
        var fingerprint = CreateFingerprint(normalizedInput);

        var existing = await store.GetAsync(normalizedUserId, normalizedKey, cancellationToken);
        if (existing is not null)
        {
            var replay = CreateReplay(existing, fingerprint);
            await EnsureDraftCreatedEventAsync(
                existing.Draft,
                normalizedInput,
                cancellationToken);
            return replay;
        }

        var createdAtUtc = clock.UtcNow.ToUniversalTime();
        var candidate = await parser.ParseAsync(
            normalizedInput,
            DateOnly.FromDateTime(createdAtUtc.UtcDateTime),
            cancellationToken);
        var draft = validator.Validate(
            idGenerator.Create(),
            normalizedUserId,
            fingerprint,
            candidate,
            createdAtUtc,
            TransactionDraftSuggestionContext.AiNaturalLanguage);
        var stored = await store.StoreIfMissingAsync(
            normalizedUserId,
            normalizedKey,
            fingerprint,
            draft,
            cancellationToken);

        if (!string.Equals(stored.Stored.InputFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new IdempotencyConflictException();
        }

        await EnsureDraftCreatedEventAsync(
            stored.Stored.Draft,
            normalizedInput,
            cancellationToken);

        return new TransactionIntakeResult(ToResponse(stored.Stored.Draft), Replayed: !stored.Created);
    }

    private async Task EnsureDraftCreatedEventAsync(
        TransactionDraft draft,
        string normalizedInput,
        CancellationToken cancellationToken)
    {
        var gateKey = $"{draft.UserId.Length}:{draft.UserId}{draft.Id}";
        var gate = AcquireDraftCreatedEventGate(gateKey);
        try
        {
            await gate.Semaphore.WaitAsync(cancellationToken);
            try
            {
                var integrationEvent = new TransactionDraftCreatedIntegrationEvent(
                    $"draft-created-{draft.Id}",
                    $"ai-job-{draft.Id}",
                    draft.Id,
                    draft.UserId,
                    $"draft-payload-{draft.Id}",
                    draft.CreatedAtUtc);
                var stored = await draftCreationStore.StoreIfMissingAsync(
                    integrationEvent,
                    normalizedInput,
                    cancellationToken);
                if (stored.Stored.Published)
                {
                    return;
                }

                await draftCreatedPublisher.PublishAsync(
                    stored.Stored.IntegrationEvent,
                    cancellationToken);
                await draftCreationStore.MarkPublishedAsync(
                    draft.UserId,
                    draft.Id,
                    integrationEvent.EventId,
                    CancellationToken.None);
            }
            finally
            {
                gate.Semaphore.Release();
            }
        }
        finally
        {
            ReleaseDraftCreatedEventGate(gateKey, gate);
        }
    }

    private DraftCreatedEventGate AcquireDraftCreatedEventGate(string key)
    {
        while (true)
        {
            var gate = draftCreatedEventGates.GetOrAdd(
                key,
                _ => new DraftCreatedEventGate());
            if (gate.TryAddReference())
            {
                return gate;
            }
        }
    }

    private void ReleaseDraftCreatedEventGate(string key, DraftCreatedEventGate gate)
    {
        if (!gate.ReleaseReference())
        {
            return;
        }

        draftCreatedEventGates.TryRemove(key, out _);
        gate.Dispose();
    }

    private static TransactionIntakeResult CreateReplay(
        StoredTransactionDraft existing,
        string fingerprint)
    {
        if (!string.Equals(existing.InputFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new IdempotencyConflictException();
        }

        return new TransactionIntakeResult(ToResponse(existing.Draft), Replayed: true);
    }

    private static string NormalizeIdempotencyKey(string value)
    {
        var normalized = value?.Trim();
        if (normalized is null || !IdempotencyKeyPattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Idempotency key must contain 8 to 128 URL-safe opaque characters.",
                nameof(value));
        }

        return normalized;
    }

    private static string NormalizeRequired(string? value, string parameterName, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        var normalized = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
        }

        return normalized;
    }

    private static string CreateFingerprint(string normalizedInput) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedInput)));

    private static TransactionDraftResponse ToResponse(TransactionDraft draft) =>
        new(
            draft.Id,
            draft.Status,
            draft.InputSource,
            draft.Type,
            draft.Amount,
            draft.Currency,
            draft.CategoryId,
            draft.Merchant,
            draft.Date,
            draft.Confidence,
            draft.Ambiguities,
            draft.RequiresReview,
            new TransactionDraftSuggestionMetadataResponse(
                draft.Suggestion.Source,
                draft.Suggestion.SourceReferenceId,
                draft.Suggestion.OutputAuthority,
                draft.Suggestion.Confidence,
                draft.Suggestion.Ambiguities,
                draft.Suggestion.MissingFields,
                draft.Suggestion.ReviewMessage),
            draft.CreatedAtUtc,
            draft.Note);

    [GeneratedRegex("^[A-Za-z0-9._~-]{8,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdempotencyKeyPattern();

    private sealed class DraftCreatedEventGate : IDisposable
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
