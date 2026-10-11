using System.Globalization;
using System.Text.Json.Serialization;
using FinancialAssistant.Category.Application.Abstractions;
using FinancialAssistant.Category.Contracts;
using FinancialAssistant.Category.Infrastructure.Storage;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Category.Infrastructure.Events;

public sealed class PostgresCategoryEventPublisher(CategoryPersistenceContext context) : ICategoryEventPublisher
{
    public async Task PublishAsync(CategoryUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        Validate(integrationEvent);
        await context.WithRecordsAsync(integrationEvent.UserId, async (records, _) =>
        {
            if (await records.GetAsync("deleted_catalogs", integrationEvent.UserId, "marker") is not null)
            { throw new StorageUnavailableException(); }
            var id = Key(integrationEvent);
            var existing = await records.GetAsync("category_outbox", "category", id);
            if (existing is not null)
            {
                if (OwnedRecordCodec.Decode<CategoryOutboxMessage>(1, existing).Event != integrationEvent)
                { throw new StorageUnavailableException(); }
                return true;
            }
            await records.InsertAsync("category_outbox", "category", id,
                OwnedRecordCodec.Encode(1, new CategoryOutboxMessage(Guid.NewGuid().ToString("D"), integrationEvent, null)));
            return true;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<CategoryOutboxMessage>> ReadPendingAsync(CancellationToken cancellationToken = default) =>
        context.WithRecordsAsync<IReadOnlyList<CategoryOutboxMessage>>("outbox-maintenance", async (records, _) =>
        {
            var scan = await records.GetAsync("outbox_scan", "category", "pending");
            var after = scan is null ? null : OwnedRecordCodec.Decode<ScanCursor>(1, scan).After;
            var pending = new List<CategoryOutboxMessage>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var wrapped = false;
            for (var page = 0; page < 10 && pending.Count < 25; page++)
            {
                var items = await records.ReadPageAsync("category_outbox", "category", after);
                if (items.Count == 0)
                {
                    after = null;
                    if (wrapped) { break; }
                    wrapped = true;
                    continue;
                }
                foreach (var item in items)
                {
                    var message = Message(new DurableRecord(item.Revision, item.Payload));
                    after = item.Cursor;
                    if (message.PublishedAtUtc is null && seen.Add(message.EventId)) { pending.Add(message); }
                    if (pending.Count == 25) { break; }
                }
            }
            var encoded = OwnedRecordCodec.Encode(1, new ScanCursor(after));
            if (scan is null) { await records.InsertAsync("outbox_scan", "category", "pending", encoded); }
            else { await records.ReplaceAsync("outbox_scan", "category", "pending", scan.Revision, encoded); }
            return pending;
        }, cancellationToken);

    public async Task MarkPublishedAsync(CategoryOutboxMessage expected, DateTimeOffset publishedAtUtc, CancellationToken cancellationToken = default)
    {
        await context.WithRecordsAsync(expected.Event.UserId, async (records, _) =>
        {
            var id = Key(expected.Event);
            var existing = await records.GetAsync("category_outbox", "category", id) ?? throw new StorageUnavailableException();
            var message = Message(existing);
            if (message.EventId != expected.EventId || message.Event != expected.Event) { throw new StorageUnavailableException(); }
            if (message.PublishedAtUtc is null)
            {
                await records.ReplaceAsync("category_outbox", "category", id, existing.Revision,
                    OwnedRecordCodec.Encode(1, message with { PublishedAtUtc = publishedAtUtc }));
            }
            return true;
        }, cancellationToken);
    }

    public Task<int> CleanupPublishedBatchAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken = default) =>
        context.WithRecordsAsync("outbox-maintenance", async (records, _) =>
        {
            var scan = await records.GetAsync("outbox_scan", "category", "retention");
            var after = scan is null ? null : OwnedRecordCodec.Decode<ScanCursor>(1, scan).After;
            var items = await records.ReadPageAsync("category_outbox", "category", after, 25);
            var count = 0;
            foreach (var item in items)
            {
                var message = Message(new DurableRecord(item.Revision, item.Payload));
                if (message.PublishedAtUtc is not null && nowUtc >= message.PublishedAtUtc.Value.AddDays(30))
                { await records.DeletePageItemAsync("category_outbox", "category", item.Cursor, item.Revision); count++; }
            }
            var cursor = new ScanCursor(items.Count < 25 ? null : items[^1].Cursor);
            if (scan is null) { await records.InsertAsync("outbox_scan", "category", "retention", OwnedRecordCodec.Encode(1, cursor)); }
            else { await records.ReplaceAsync("outbox_scan", "category", "retention", scan.Revision, OwnedRecordCodec.Encode(1, cursor)); }
            return count;
        }, cancellationToken);

    private static CategoryOutboxMessage Message(DurableRecord record)
    {
        var message = OwnedRecordCodec.Decode<CategoryOutboxMessage>(1, record);
        if (!Guid.TryParseExact(message.EventId, "D", out _))
        { throw new StorageUnavailableException(); }
        Validate(message.Event);
        return message;
    }
    private static void Validate(CategoryUpdatedIntegrationEvent value)
    {
        if (value is null || value.Version < 1 || value.ChangeType != "aliases_replaced" || value.OccurredAtUtc == default
            || !Canonical(value.UserId, 128) || !Canonical(value.CategoryId, 100) || !Canonical(value.CorrelationId, 256))
        { throw new StorageUnavailableException(); }
    }
    private static bool Canonical(string value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum
        && value == value.Trim() && !value.Any(char.IsControl);
    private static string Key(CategoryUpdatedIntegrationEvent integrationEvent) =>
        $"{integrationEvent.UserId.Length}:{integrationEvent.UserId}{integrationEvent.CategoryId.Length}:{integrationEvent.CategoryId}{integrationEvent.Version.ToString(CultureInfo.InvariantCulture)}";
    private sealed record ScanCursor(string? After);
}

public sealed record CategoryOutboxMessage([property: JsonRequired] string EventId, [property: JsonRequired] CategoryUpdatedIntegrationEvent Event,
    [property: JsonRequired] DateTimeOffset? PublishedAtUtc);
