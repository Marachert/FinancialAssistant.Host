using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Domain;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

// Native files alone must not acknowledge uploads backed by process-local metadata.
internal sealed class PendingNativeReceiptMetadata : IReceiptMetadataStore, IOcrProcessingStore
{
    public Task<ReceiptFileMetadata?> GetByIdempotencyKeyAsync(string userId, string key, CancellationToken token) => Unavailable<ReceiptFileMetadata?>();
    Task<ReceiptFileMetadata?> IReceiptMetadataStore.GetAsync(string userId, string receiptId, CancellationToken token) => Unavailable<ReceiptFileMetadata?>();
    public Task<bool> AddAsync(string key, ReceiptFileMetadata value, CancellationToken token) => Unavailable<bool>();
    public Task MarkReceiptUploadedPublishedAsync(string userId, string receiptId, string eventId, CancellationToken token) => Unavailable<bool>();
    Task<StoredOcrProcessing?> IOcrProcessingStore.GetAsync(string userId, string receiptId, CancellationToken token) => Unavailable<StoredOcrProcessing?>();
    public Task<StoredOcrProcessing> StoreIfMissingAsync(ReceiptOcrMetadata metadata, OcrCompletedIntegrationEvent? value, CancellationToken token) => Unavailable<StoredOcrProcessing>();
    public Task MarkPublishedAsync(string userId, string receiptId, string eventId, CancellationToken token) => Unavailable<bool>();
    private static Task<T> Unavailable<T>() => Task.FromException<T>(new NativeReceiptStorageException());
}
