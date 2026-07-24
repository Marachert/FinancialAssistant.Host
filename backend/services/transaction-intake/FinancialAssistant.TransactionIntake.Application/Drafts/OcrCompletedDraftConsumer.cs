using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Application.Drafts;

public sealed class OcrCompletedDraftConsumer : IOcrCompletedConsumer
{
    private readonly ITransactionDraftStore store;
    private readonly ITransactionIntakeClock clock;
    private readonly ITransactionDraftIdGenerator idGenerator;
    private readonly TransactionDraftValidator validator;

    public OcrCompletedDraftConsumer(
        ITransactionDraftStore store,
        ITransactionIntakeClock clock,
        ITransactionDraftIdGenerator idGenerator,
        TransactionDraftValidator validator)
    {
        this.store = store;
        this.clock = clock;
        this.idGenerator = idGenerator;
        this.validator = validator;
    }

    public async Task ConsumeAsync(
        OcrCompletedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        Validate(integrationEvent);
        var idempotencyKey = $"ocr-{integrationEvent.ReceiptId}";
        var fingerprint = CreateFingerprint(integrationEvent);
        var existing = await store.GetAsync(
            integrationEvent.UserId,
            idempotencyKey,
            cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.InputFingerprint, fingerprint, StringComparison.Ordinal))
            {
                throw new OcrCompletedDraftConflictException();
            }

            return;
        }

        var candidate = new ParsedTransactionCandidate(
            integrationEvent.TransactionType,
            integrationEvent.Amount,
            integrationEvent.Currency,
            integrationEvent.CategoryId,
            integrationEvent.Merchant,
            integrationEvent.Date,
            integrationEvent.Confidence);
        var createdAtUtc = clock.UtcNow.ToUniversalTime();
        var draft = validator.Validate(
            idGenerator.Create(),
            integrationEvent.UserId,
            fingerprint,
            candidate,
            createdAtUtc,
            new TransactionDraftSuggestionContext(
                TransactionDraftSuggestionSources.ReceiptOcr,
                integrationEvent.ReceiptId,
                NormalizeAmbiguities(integrationEvent.Ambiguities),
                MissingFields: Array.Empty<string>()));
        var stored = await store.StoreIfMissingAsync(
            integrationEvent.UserId,
            idempotencyKey,
            fingerprint,
            draft,
            cancellationToken);
        if (!string.Equals(stored.Stored.InputFingerprint, fingerprint, StringComparison.Ordinal))
        {
            throw new OcrCompletedDraftConflictException();
        }
    }

    private static string CreateFingerprint(OcrCompletedIntegrationEvent integrationEvent)
    {
        var canonical = JsonSerializer.Serialize(
            new
            {
                integrationEvent.ReceiptId,
                integrationEvent.TransactionType,
                integrationEvent.Amount,
                integrationEvent.Currency,
                integrationEvent.CategoryId,
                integrationEvent.Merchant,
                integrationEvent.Date,
                integrationEvent.Confidence,
                Ambiguities = NormalizeAmbiguities(integrationEvent.Ambiguities)
            });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static void Validate(OcrCompletedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        if (!string.Equals(
                integrationEvent.EventType,
                OcrCompletedIntegrationEvent.Name,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(integrationEvent.EventId) ||
            string.IsNullOrWhiteSpace(integrationEvent.ReceiptId) ||
            string.IsNullOrWhiteSpace(integrationEvent.UserId) ||
            integrationEvent.EventId.Length > 200 ||
            integrationEvent.ReceiptId.Length > 100 ||
            integrationEvent.UserId.Length > 200 ||
            integrationEvent.Confidence is < 0 or > 1)
        {
            throw new InvalidOperationException("OCR completed event is invalid.");
        }

        _ = NormalizeAmbiguities(integrationEvent.Ambiguities);
    }

    private static string[] NormalizeAmbiguities(IReadOnlyList<string> ambiguities)
    {
        if (ambiguities is null || ambiguities.Count > 50)
        {
            throw new InvalidOperationException("OCR ambiguity metadata is invalid.");
        }

        var normalized = ambiguities
            .Select(value => value?.Trim().ToLowerInvariant())
            .ToArray();
        if (normalized.Any(value =>
                string.IsNullOrWhiteSpace(value) ||
                value.Length > 64 ||
                value.Any(character =>
                    !(char.IsLower(character) || char.IsDigit(character) || character == '_'))))
        {
            throw new InvalidOperationException("OCR ambiguity metadata is invalid.");
        }

        return normalized
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed class OcrCompletedDraftConflictException : InvalidOperationException
{
    public OcrCompletedDraftConflictException()
        : base("OCR completion conflicts with the stored draft.")
    {
    }
}
