using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Domain;

namespace FinancialAssistant.ReceiptProcessing.Application.Abstractions;

public interface IReceiptObjectStore
{
    Task StoreAsync(
        string receiptId,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string receiptId, CancellationToken cancellationToken);
}

public interface IReceiptMetadataStore
{
    Task<ReceiptFileMetadata?> GetByIdempotencyKeyAsync(
        string userId,
        string idempotencyKey,
        CancellationToken cancellationToken);

    Task<ReceiptFileMetadata?> GetAsync(
        string userId,
        string receiptId,
        CancellationToken cancellationToken);

    Task<bool> AddAsync(
        string idempotencyKey,
        ReceiptFileMetadata metadata,
        CancellationToken cancellationToken);

    Task MarkReceiptUploadedPublishedAsync(
        string userId,
        string receiptId,
        string eventId,
        CancellationToken cancellationToken);
}

public interface IOcrProcessingStore
{
    Task<StoredOcrProcessing?> GetAsync(
        string userId,
        string receiptId,
        CancellationToken cancellationToken);

    Task<StoredOcrProcessing> StoreIfMissingAsync(
        ReceiptOcrMetadata metadata,
        OcrCompletedIntegrationEvent? integrationEvent,
        CancellationToken cancellationToken);

    Task MarkPublishedAsync(
        string userId,
        string receiptId,
        string eventId,
        CancellationToken cancellationToken);
}

public interface IReceiptUploadedPublisher
{
    Task PublishAsync(
        ReceiptUploadedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}

public interface IOcrCompletedPublisher
{
    Task PublishAsync(
        OcrCompletedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken);
}

public interface IOcrProvider
{
    Task<OcrExtractionResult> ExtractAsync(
        Stream receiptImage,
        string contentType,
        CancellationToken cancellationToken);
}

public interface IOcrProviderClient
{
    Task<OcrExtractionResult> ExtractAsync(
        ReadOnlyMemory<byte> receiptImage,
        string contentType,
        CancellationToken cancellationToken);
}

public interface IOcrCandidateNormalizer
{
    NormalizedReceiptCandidate Normalize(OcrExtractionResult extraction);
}

public interface IReceiptProcessingClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IReceiptProcessingIdGenerator
{
    string CreateReceiptId();

    string CreateEventId();
}

public sealed record StoredOcrProcessing(
    ReceiptOcrMetadata Metadata,
    OcrCompletedIntegrationEvent? IntegrationEvent);

public sealed record OcrExtractionResult(
    string ExtractedText,
    decimal Confidence,
    IReadOnlyList<string> Ambiguities);

public sealed record ReceiptLineItemCandidate(
    string Description,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal? TotalAmount,
    string? Currency,
    decimal Confidence,
    IReadOnlyList<string> Ambiguities);

public static class ReceiptCandidateAuthority
{
    public const string Suggestion = "suggestion";
}

public sealed record NormalizedReceiptCandidate(
    string? TransactionType,
    decimal? Amount,
    string? Currency,
    string? CategoryId,
    string? Merchant,
    DateOnly? Date,
    decimal? TaxAmount,
    IReadOnlyList<ReceiptLineItemCandidate> LineItems,
    decimal Confidence,
    IReadOnlyList<string> Ambiguities)
{
    public string OutputAuthority => ReceiptCandidateAuthority.Suggestion;

    public bool RequiresReview => true;
}
