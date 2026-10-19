using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

public sealed class PostgresOcrProcessingStore(ReceiptPersistenceContext context) : IOcrProcessingStore
{
    public Task<StoredOcrProcessing?> GetAsync(string userId, string receiptId, CancellationToken token)
    {
        ReceiptRecordCodec.Identifier(receiptId);
        return context.WithRecordsAsync<StoredOcrProcessing?>(userId, async (records, _) =>
        {
            var durable = await records.GetAsync("ocr_results", userId, receiptId);
            return durable is null ? null : Decode(durable, userId, receiptId);
        }, token);
    }

    public Task<StoredOcrProcessing> StoreIfMissingAsync(ReceiptOcrMetadata metadata,
        OcrCompletedIntegrationEvent? integrationEvent, CancellationToken token)
    {
        var value = new StoredOcrProcessing(metadata, integrationEvent);
        ReceiptRecordCodec.Processing(value);
        if (metadata.OcrCompletedPublished) { throw new ArgumentException("New OCR result cannot claim delivery."); }
        return context.WithRecordsAsync(metadata.UserId, async (records, _) =>
        {
            var fileRecord = await records.GetAsync("receipts", metadata.UserId, metadata.ReceiptId) ?? throw new StorageUnavailableException();
            var file = ReceiptRecordCodec.Decode<ReceiptFileMetadata>(fileRecord, ReceiptRecordCodec.File);
            if (file.UserId != metadata.UserId || file.ReceiptId != metadata.ReceiptId ||
                file.ReceiptUploadedEventId != metadata.Audit.RequestId || file.UploadedAtUtc > metadata.CompletedAtUtc ||
                file.SizeBytes != metadata.Audit.ProviderUsage.RequestBytes)
            { throw new StorageUnavailableException(); }
            var existing = await records.GetAsync("ocr_results", metadata.UserId, metadata.ReceiptId);
            if (existing is not null) { return Decode(existing, metadata.UserId, metadata.ReceiptId); }
            await records.InsertAsync("ocr_results", metadata.UserId, metadata.ReceiptId, OwnedRecordCodec.Encode(1, value));
            if (integrationEvent is not null) { await PostgresReceiptOutbox.EnqueueAsync(records, ReceiptOutboxMessage.For(integrationEvent)); }
            return value;
        }, token);
    }

    public Task MarkPublishedAsync(string userId, string receiptId, string eventId, CancellationToken token) =>
        context.WithRecordsAsync(userId, async (records, _) =>
        {
            var durable = await records.GetAsync("ocr_results", userId, receiptId) ?? throw new StorageUnavailableException();
            var value = Decode(durable, userId, receiptId);
            if (value.IntegrationEvent?.EventId != eventId) { throw new StorageUnavailableException(); }
            await PostgresReceiptOutbox.VerifyAcknowledgedAsync(records, "completed:" + eventId, userId, receiptId);
            if (!value.Metadata.OcrCompletedPublished)
            {
                await records.ReplaceAsync("ocr_results", userId, receiptId,
                durable.Revision, OwnedRecordCodec.Encode(1, value with { Metadata = value.Metadata with { OcrCompletedPublished = true } }));
            }
            return true;
        }, token);

    private static StoredOcrProcessing Decode(DurableRecord durable, string owner, string id)
    {
        var value = ReceiptRecordCodec.Decode<StoredOcrProcessing>(durable, ReceiptRecordCodec.Processing);
        if (value.Metadata.UserId != owner || value.Metadata.ReceiptId != id) { throw new StorageUnavailableException(); }
        return value;
    }
}
