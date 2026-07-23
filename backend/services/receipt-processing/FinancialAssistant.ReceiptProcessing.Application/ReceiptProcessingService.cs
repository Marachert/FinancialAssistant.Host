using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Domain;

namespace FinancialAssistant.ReceiptProcessing.Application;

public sealed partial class ReceiptProcessingService : IReceiptProcessingService
{
    public const long MaximumReceiptSizeBytes = 10 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, Func<byte[], bool>> MediaValidators =
        new Dictionary<string, Func<byte[], bool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = bytes =>
                bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            ["image/png"] = bytes =>
                bytes.Length >= 8 &&
                bytes.AsSpan(0, 8).SequenceEqual(
                    new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ["image/webp"] = bytes =>
                bytes.Length >= 12 &&
                bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        };

    private readonly IReceiptObjectStore objectStore;
    private readonly IReceiptMetadataStore metadataStore;
    private readonly IOcrProcessingStore ocrStore;
    private readonly IReceiptUploadedPublisher publisher;
    private readonly IReceiptProcessingClock clock;
    private readonly IReceiptProcessingIdGenerator idGenerator;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> uploadLocks =
        new(StringComparer.Ordinal);

    public ReceiptProcessingService(
        IReceiptObjectStore objectStore,
        IReceiptMetadataStore metadataStore,
        IOcrProcessingStore ocrStore,
        IReceiptUploadedPublisher publisher,
        IReceiptProcessingClock clock,
        IReceiptProcessingIdGenerator idGenerator)
    {
        this.objectStore = objectStore;
        this.metadataStore = metadataStore;
        this.ocrStore = ocrStore;
        this.publisher = publisher;
        this.clock = clock;
        this.idGenerator = idGenerator;
    }

    public async Task<ReceiptUploadResult> UploadAsync(
        string userId,
        string idempotencyKey,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeRequired(userId, nameof(userId), 200);
        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey);
        var normalizedContentType = NormalizeContentType(contentType);
        ArgumentNullException.ThrowIfNull(content);

        var bytes = await ReadContentAsync(content, cancellationToken);
        try
        {
            if (!MediaValidators[normalizedContentType](bytes))
            {
                throw new UnsupportedReceiptMediaTypeException();
            }

            var digest = Convert.ToHexString(SHA256.HashData(bytes));
            var lockKey = $"{normalizedUserId}\n{normalizedKey}";
            var uploadLock = uploadLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
            await uploadLock.WaitAsync(cancellationToken);
            try
            {
                var existing = await metadataStore.GetByIdempotencyKeyAsync(
                    normalizedUserId,
                    normalizedKey,
                    cancellationToken);
                if (existing is not null)
                {
                    if (!string.Equals(existing.ContentDigest, digest, StringComparison.Ordinal))
                    {
                        throw new ReceiptIdempotencyConflictException();
                    }

                    await PublishIfPendingAsync(existing, cancellationToken);
                    return new ReceiptUploadResult(
                        await ToResponseAsync(existing, cancellationToken),
                        Replayed: true);
                }

                var receiptId = idGenerator.CreateReceiptId();
                var uploadedAtUtc = clock.UtcNow.ToUniversalTime();
                await objectStore.StoreAsync(receiptId, bytes, cancellationToken);
                var metadata = new ReceiptFileMetadata(
                    receiptId,
                    normalizedUserId,
                    normalizedContentType,
                    bytes.LongLength,
                    digest,
                    uploadedAtUtc,
                    idGenerator.CreateEventId(),
                    ReceiptUploadedPublished: false);
                if (!await metadataStore.AddAsync(normalizedKey, metadata, cancellationToken))
                {
                    throw new InvalidOperationException("Receipt metadata could not be stored.");
                }

                await PublishIfPendingAsync(metadata, cancellationToken);
                var stored = await metadataStore.GetAsync(
                    normalizedUserId,
                    receiptId,
                    cancellationToken) ?? metadata;
                return new ReceiptUploadResult(
                    await ToResponseAsync(stored, cancellationToken),
                    Replayed: false);
            }
            finally
            {
                uploadLock.Release();
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public async Task<ReceiptResponse?> GetAsync(
        string userId,
        string receiptId,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeRequired(userId, nameof(userId), 200);
        var normalizedReceiptId = NormalizeRequired(receiptId, nameof(receiptId), 100);
        var metadata = await metadataStore.GetAsync(
            normalizedUserId,
            normalizedReceiptId,
            cancellationToken);
        return metadata is null ? null : await ToResponseAsync(metadata, cancellationToken);
    }

    private async Task PublishIfPendingAsync(
        ReceiptFileMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (metadata.ReceiptUploadedPublished)
        {
            return;
        }

        var integrationEvent = new ReceiptUploadedIntegrationEvent(
            ReceiptUploadedIntegrationEvent.Name,
            metadata.ReceiptUploadedEventId,
            metadata.ReceiptId,
            metadata.UserId,
            metadata.ContentType,
            metadata.UploadedAtUtc);
        await publisher.PublishAsync(integrationEvent, cancellationToken);
        await metadataStore.MarkReceiptUploadedPublishedAsync(
            metadata.UserId,
            metadata.ReceiptId,
            integrationEvent.EventId,
            CancellationToken.None);
    }

    private async Task<ReceiptResponse> ToResponseAsync(
        ReceiptFileMetadata metadata,
        CancellationToken cancellationToken)
    {
        var ocr = await ocrStore.GetAsync(metadata.UserId, metadata.ReceiptId, cancellationToken);
        return new ReceiptResponse(
            metadata.ReceiptId,
            ocr?.Metadata.Status ?? ReceiptProcessingStatuses.Uploaded,
            metadata.ContentType,
            metadata.SizeBytes,
            ocr?.Metadata.Confidence,
            ocr?.Metadata.Ambiguities ?? Array.Empty<string>(),
            metadata.UploadedAtUtc);
    }

    private static async Task<byte[]> ReadContentAsync(
        Stream content,
        CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[64 * 1024];
        while (true)
        {
            var read = await content.ReadAsync(chunk, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaximumReceiptSizeBytes)
            {
                throw new ReceiptFileTooLargeException(MaximumReceiptSizeBytes);
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }

        if (buffer.Length == 0)
        {
            throw new ArgumentException("Receipt image content is required.", nameof(content));
        }

        return buffer.ToArray();
    }

    private static string NormalizeContentType(string value)
    {
        var normalized = value?.Split(';', 2)[0].Trim();
        if (normalized is null || !MediaValidators.ContainsKey(normalized))
        {
            throw new UnsupportedReceiptMediaTypeException();
        }

        return normalized.ToLowerInvariant();
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
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
        {
            throw new ArgumentException("A valid value is required.", parameterName);
        }

        return normalized;
    }

    [GeneratedRegex("^[A-Za-z0-9._~-]{8,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdempotencyKeyPattern();
}
