using System.Text.Json.Serialization;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

public sealed class PostgresReceiptMetadataStore(ReceiptPersistenceContext context, NativeReceiptObjectStore objects)
    : IReceiptMetadataStore
{
    public Task<ReceiptFileMetadata?> GetByIdempotencyKeyAsync(string userId, string key, CancellationToken token)
    {
        ReceiptRecordCodec.Identifier(key);
        return context.WithRecordsAsync<ReceiptFileMetadata?>(userId, async (records, deadline) =>
        {
            var index = await records.GetAsync("upload_keys", userId, key);
            if (index is null) { return null; }
            var reference = ReceiptRecordCodec.Decode<UploadReference>(index, r =>
            { ReceiptRecordCodec.Identifier(r.ReceiptId); ReceiptRecordCodec.Identifier(r.EventId); });
            var durable = await records.GetAsync("receipts", userId, reference.ReceiptId) ?? throw new StorageUnavailableException();
            var value = Decode(durable, userId, reference.ReceiptId);
            if (value.ReceiptUploadedEventId != reference.EventId) { throw new StorageUnavailableException(); }
            await VerifyObjectAsync(value, deadline);
            return value;
        }, token);
    }

    public Task<ReceiptFileMetadata?> GetAsync(string userId, string receiptId, CancellationToken token)
    {
        ReceiptRecordCodec.Identifier(receiptId);
        return context.WithRecordsAsync<ReceiptFileMetadata?>(userId, async (records, _) =>
        {
            var record = await records.GetAsync("receipts", userId, receiptId);
            return record is null ? null : Decode(record, userId, receiptId);
        }, token);
    }

    public Task<bool> AddAsync(string key, ReceiptFileMetadata value, CancellationToken token)
    {
        ReceiptRecordCodec.Identifier(key); ReceiptRecordCodec.File(value);
        if (value.ReceiptUploadedPublished) { throw new ArgumentException("New upload cannot claim delivery."); }
        return context.WithRecordsAsync(value.UserId, async (records, deadline) =>
        {
            if (await records.GetAsync("upload_keys", value.UserId, key) is not null) { return false; }
            await VerifyObjectAsync(value, deadline);
            if (await records.GetAsync("receipt_owners", "receipt", value.ReceiptId) is not null)
            { throw new StorageConflictException(); }
            await records.InsertAsync("receipt_owners", "receipt", value.ReceiptId, OwnedRecordCodec.Encode(1, new Owner(value.UserId)));
            await records.InsertAsync("receipts", value.UserId, value.ReceiptId, OwnedRecordCodec.Encode(1, value));
            await records.InsertAsync("upload_keys", value.UserId, key,
                OwnedRecordCodec.Encode(1, new UploadReference(value.ReceiptId, value.ReceiptUploadedEventId)));
            await PostgresReceiptOutbox.EnqueueAsync(records, ReceiptOutboxMessage.For(value));
            return true;
        }, token);
    }

    public Task MarkReceiptUploadedPublishedAsync(string userId, string receiptId, string eventId, CancellationToken token) =>
        context.WithRecordsAsync(userId, async (records, _) =>
        {
            var durable = await records.GetAsync("receipts", userId, receiptId) ?? throw new StorageUnavailableException();
            var value = Decode(durable, userId, receiptId);
            if (value.ReceiptUploadedEventId != eventId) { throw new StorageUnavailableException(); }
            await PostgresReceiptOutbox.VerifyAcknowledgedAsync(records, "uploaded:" + eventId, userId, receiptId);
            if (!value.ReceiptUploadedPublished)
            {
                await records.ReplaceAsync("receipts", userId, receiptId,
                durable.Revision, OwnedRecordCodec.Encode(1, value with { ReceiptUploadedPublished = true }));
            }
            return true;
        }, token);

    private async Task VerifyObjectAsync(ReceiptFileMetadata value, CancellationToken token)
    {
        var info = await objects.GetInfoAsync(value.ReceiptId, token);
        if (info is null || info.DeletedOrExpired || info.SizeBytes != value.SizeBytes || info.ContentDigest != value.ContentDigest)
        { throw new StorageUnavailableException(); }
    }

    private static ReceiptFileMetadata Decode(DurableRecord durable, string owner, string id)
    {
        var value = ReceiptRecordCodec.Decode<ReceiptFileMetadata>(durable, ReceiptRecordCodec.File);
        if (value.UserId != owner || value.ReceiptId != id) { throw new StorageUnavailableException(); }
        return value;
    }

    private sealed record UploadReference([property: JsonRequired] string ReceiptId, [property: JsonRequired] string EventId);
    private sealed record Owner([property: JsonRequired] string UserId);
}
