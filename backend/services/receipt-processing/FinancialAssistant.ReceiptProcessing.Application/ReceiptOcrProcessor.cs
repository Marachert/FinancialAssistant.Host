using System.Collections.Concurrent;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Domain;

namespace FinancialAssistant.ReceiptProcessing.Application;

public sealed class ReceiptOcrProcessor : IReceiptUploadedConsumer
{
    private readonly IReceiptObjectStore objectStore;
    private readonly IReceiptMetadataStore receiptMetadataStore;
    private readonly IOcrProcessingStore processingStore;
    private readonly IOcrProvider ocrProvider;
    private readonly IOcrCandidateNormalizer normalizer;
    private readonly IOcrCompletedPublisher publisher;
    private readonly IReceiptProcessingClock clock;
    private readonly IReceiptProcessingIdGenerator idGenerator;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> processingLocks =
        new(StringComparer.Ordinal);

    public ReceiptOcrProcessor(
        IReceiptObjectStore objectStore,
        IReceiptMetadataStore receiptMetadataStore,
        IOcrProcessingStore processingStore,
        IOcrProvider ocrProvider,
        IOcrCandidateNormalizer normalizer,
        IOcrCompletedPublisher publisher,
        IReceiptProcessingClock clock,
        IReceiptProcessingIdGenerator idGenerator)
    {
        this.objectStore = objectStore;
        this.receiptMetadataStore = receiptMetadataStore;
        this.processingStore = processingStore;
        this.ocrProvider = ocrProvider;
        this.normalizer = normalizer;
        this.publisher = publisher;
        this.clock = clock;
        this.idGenerator = idGenerator;
    }

    public async Task ConsumeAsync(
        ReceiptUploadedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        Validate(integrationEvent);
        var startedAtUtc = clock.UtcNow.ToUniversalTime();
        var processingLock = processingLocks.GetOrAdd(
            $"{integrationEvent.UserId}\n{integrationEvent.ReceiptId}",
            _ => new SemaphoreSlim(1, 1));
        await processingLock.WaitAsync(cancellationToken);
        try
        {
            var receiptMetadata = await receiptMetadataStore.GetAsync(
                integrationEvent.UserId,
                integrationEvent.ReceiptId,
                cancellationToken);
            if (receiptMetadata is null ||
                !string.Equals(
                    receiptMetadata.ReceiptUploadedEventId,
                    integrationEvent.EventId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    receiptMetadata.ContentType,
                    integrationEvent.ContentType,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Receipt uploaded event does not match stored receipt metadata.");
            }

            var existing = await processingStore.GetAsync(
                integrationEvent.UserId,
                integrationEvent.ReceiptId,
                cancellationToken);
            if (existing is not null)
            {
                await PublishIfPendingAsync(existing, cancellationToken);
                return;
            }

            await using var image = await objectStore.OpenReadAsync(
                integrationEvent.ReceiptId,
                cancellationToken);
            if (image is null)
            {
                throw new InvalidOperationException("Receipt image storage is inconsistent.");
            }

            OcrExtractionResult extraction;
            try
            {
                extraction = await ocrProvider.ExtractAsync(
                    image,
                    integrationEvent.ContentType,
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OcrProviderException exception)
            {
                var failed = CreateMetadata(
                    integrationEvent,
                    startedAtUtc,
                    ReceiptProcessingStatuses.OcrFailed,
                    confidence: null,
                    new[] { "ocr_provider_failed" },
                    exception.ErrorCode);
                await processingStore.StoreIfMissingAsync(failed, null, CancellationToken.None);
                return;
            }
            catch
            {
                var failed = CreateMetadata(
                    integrationEvent,
                    startedAtUtc,
                    ReceiptProcessingStatuses.OcrFailed,
                    confidence: null,
                    new[] { "ocr_provider_failed" },
                    OcrProviderErrorCodes.ProviderFailure);
                await processingStore.StoreIfMissingAsync(failed, null, CancellationToken.None);
                return;
            }

            NormalizedReceiptCandidate candidate;
            try
            {
                candidate = normalizer.Normalize(extraction);
            }
            catch
            {
                var failed = CreateMetadata(
                    integrationEvent,
                    startedAtUtc,
                    ReceiptProcessingStatuses.OcrFailed,
                    confidence: null,
                    new[] { "ocr_output_invalid" },
                    "ocr_output_invalid");
                await processingStore.StoreIfMissingAsync(failed, null, CancellationToken.None);
                return;
            }

            var completedAtUtc = clock.UtcNow.ToUniversalTime();
            var completedEvent = new OcrCompletedIntegrationEvent(
                OcrCompletedIntegrationEvent.Name,
                idGenerator.CreateEventId(),
                integrationEvent.ReceiptId,
                integrationEvent.UserId,
                candidate.TransactionType,
                candidate.Amount,
                candidate.Currency,
                candidate.CategoryId,
                candidate.Merchant,
                candidate.Date,
                candidate.Confidence,
                candidate.Ambiguities,
                completedAtUtc);
            var metadata = CreateMetadata(
                integrationEvent,
                startedAtUtc,
                ReceiptProcessingStatuses.OcrCompleted,
                candidate.Confidence,
                candidate.Ambiguities,
                failureCategory: null,
                completedAtUtc);
            var stored = await processingStore.StoreIfMissingAsync(
                metadata,
                completedEvent,
                cancellationToken);
            await PublishIfPendingAsync(stored, cancellationToken);
        }
        finally
        {
            processingLock.Release();
        }
    }

    private ReceiptOcrMetadata CreateMetadata(
        ReceiptUploadedIntegrationEvent integrationEvent,
        DateTimeOffset startedAtUtc,
        string status,
        decimal? confidence,
        IReadOnlyList<string> ambiguities,
        string? failureCategory,
        DateTimeOffset? completedAtUtc = null)
    {
        var completed = (completedAtUtc ?? clock.UtcNow).ToUniversalTime();
        var durationMilliseconds = Math.Max(
            0,
            (long)(completed - startedAtUtc).TotalMilliseconds);
        return new ReceiptOcrMetadata(
            integrationEvent.ReceiptId,
            integrationEvent.UserId,
            status,
            confidence,
            ambiguities,
            new OcrProcessingAuditMetadata(
                integrationEvent.EventId,
                ocrProvider.ProviderName,
                ocrProvider.ModelKey,
                durationMilliseconds,
                confidence,
                failureCategory,
                integrationEvent.EventId),
            completed,
            OcrCompletedPublished: false);
    }

    private async Task PublishIfPendingAsync(
        StoredOcrProcessing stored,
        CancellationToken cancellationToken)
    {
        if (stored.Metadata.OcrCompletedPublished || stored.IntegrationEvent is null)
        {
            return;
        }

        await publisher.PublishAsync(stored.IntegrationEvent, cancellationToken);
        await processingStore.MarkPublishedAsync(
            stored.Metadata.UserId,
            stored.Metadata.ReceiptId,
            stored.IntegrationEvent.EventId,
            CancellationToken.None);
    }

    private static void Validate(ReceiptUploadedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        if (!string.Equals(
                integrationEvent.EventType,
                ReceiptUploadedIntegrationEvent.Name,
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(integrationEvent.EventId) ||
            string.IsNullOrWhiteSpace(integrationEvent.ReceiptId) ||
            string.IsNullOrWhiteSpace(integrationEvent.UserId) ||
            string.IsNullOrWhiteSpace(integrationEvent.ContentType) ||
            integrationEvent.EventId.Length > 200 ||
            integrationEvent.ReceiptId.Length > 100 ||
            integrationEvent.UserId.Length > 200 ||
            integrationEvent.ContentType.Length > 100)
        {
            throw new InvalidOperationException("Receipt uploaded event is invalid.");
        }
    }
}
