using System.Collections.Concurrent;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Domain;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

public sealed class InMemoryOcrProcessingStore : IOcrProcessingStore
{
    private readonly ConcurrentDictionary<string, StoredOcrProcessing> processingByReceipt =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<ReceiptOcrMetadata> Records =>
        processingByReceipt.Values.Select(value => value.Metadata).ToArray();

    public Task<StoredOcrProcessing?> GetAsync(
        string userId,
        string receiptId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        processingByReceipt.TryGetValue(CreateKey(userId, receiptId), out var stored);
        return Task.FromResult(stored);
    }

    public Task<StoredOcrProcessing> StoreIfMissingAsync(
        ReceiptOcrMetadata metadata,
        OcrCompletedIntegrationEvent? integrationEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stored = processingByReceipt.GetOrAdd(
            CreateKey(metadata.UserId, metadata.ReceiptId),
            _ => new StoredOcrProcessing(metadata, integrationEvent));
        return Task.FromResult(stored);
    }

    public Task MarkPublishedAsync(
        string userId,
        string receiptId,
        string eventId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        processingByReceipt.AddOrUpdate(
            CreateKey(userId, receiptId),
            _ => throw new InvalidOperationException("OCR processing metadata does not exist."),
            (_, current) =>
            {
                if (!string.Equals(current.IntegrationEvent?.EventId, eventId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("OCR event identity does not match.");
                }

                return current with
                {
                    Metadata = current.Metadata with { OcrCompletedPublished = true }
                };
            });
        return Task.CompletedTask;
    }

    private static string CreateKey(string userId, string receiptId) => $"{userId}\n{receiptId}";
}
