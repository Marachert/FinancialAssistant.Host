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

    private readonly ITransactionInputParser parser;
    private readonly ITransactionDraftStore store;
    private readonly ITransactionIntakeClock clock;
    private readonly ITransactionDraftIdGenerator idGenerator;
    private readonly TransactionDraftValidator validator;

    public TransactionIntakeService(
        ITransactionInputParser parser,
        ITransactionDraftStore store,
        ITransactionIntakeClock clock,
        ITransactionDraftIdGenerator idGenerator,
        TransactionDraftValidator validator)
    {
        this.parser = parser;
        this.store = store;
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
            return CreateReplay(existing, fingerprint);
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
            createdAtUtc);
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

        return new TransactionIntakeResult(ToResponse(stored.Stored.Draft), Replayed: !stored.Created);
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
            TransactionDraft.Status,
            draft.Type,
            draft.Amount,
            draft.Currency,
            draft.CategoryId,
            draft.Merchant,
            draft.Date,
            draft.Confidence,
            draft.Ambiguities,
            draft.RequiresReview,
            draft.CreatedAtUtc);

    [GeneratedRegex("^[A-Za-z0-9._~-]{8,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdempotencyKeyPattern();
}
