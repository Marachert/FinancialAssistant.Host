using System.Collections.Concurrent;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Domain;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

public sealed class InMemoryReceiptMetadataStore : IReceiptMetadataStore
{
    private readonly ConcurrentDictionary<string, ReceiptFileMetadata> metadataByReceipt =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> receiptByIdempotencyKey =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<ReceiptFileMetadata> Records => metadataByReceipt.Values.ToArray();

    public Task<ReceiptFileMetadata?> GetByIdempotencyKeyAsync(
        string userId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = CreateIdempotencyLookupKey(userId, idempotencyKey);
        return Task.FromResult(
            receiptByIdempotencyKey.TryGetValue(key, out var receiptId) &&
            metadataByReceipt.TryGetValue(CreateReceiptLookupKey(userId, receiptId), out var metadata)
                ? metadata
                : null);
    }

    public Task<ReceiptFileMetadata?> GetAsync(
        string userId,
        string receiptId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        metadataByReceipt.TryGetValue(CreateReceiptLookupKey(userId, receiptId), out var metadata);
        return Task.FromResult(metadata);
    }

    public Task<bool> AddAsync(
        string idempotencyKey,
        ReceiptFileMetadata metadata,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var receiptKey = CreateReceiptLookupKey(metadata.UserId, metadata.ReceiptId);
        var idempotencyKeyHash = CreateIdempotencyLookupKey(metadata.UserId, idempotencyKey);
        if (!receiptByIdempotencyKey.TryAdd(idempotencyKeyHash, metadata.ReceiptId))
        {
            return Task.FromResult(false);
        }

        if (metadataByReceipt.TryAdd(receiptKey, metadata))
        {
            return Task.FromResult(true);
        }

        receiptByIdempotencyKey.TryRemove(idempotencyKeyHash, out _);
        return Task.FromResult(false);
    }

    public Task MarkReceiptUploadedPublishedAsync(
        string userId,
        string receiptId,
        string eventId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = CreateReceiptLookupKey(userId, receiptId);
        metadataByReceipt.AddOrUpdate(
            key,
            _ => throw new InvalidOperationException("Receipt metadata does not exist."),
            (_, current) =>
            {
                if (!string.Equals(
                        current.ReceiptUploadedEventId,
                        eventId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Receipt event identity does not match.");
                }

                return current with { ReceiptUploadedPublished = true };
            });
        return Task.CompletedTask;
    }

    private static string CreateReceiptLookupKey(string userId, string receiptId) =>
        $"{userId}\n{receiptId}";

    private static string CreateIdempotencyLookupKey(string userId, string idempotencyKey) =>
        $"{userId}\n{idempotencyKey}";
}
