using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Analytics.Infrastructure.Persistence;

public sealed record AnalyticsOutboxMessage(IntegrationEventEnvelope<AnalyticsUpdatedV1> Event, DateTimeOffset? PublishedAtUtc);

public sealed class PostgresAnalyticsEventPublisher(AnalyticsPersistenceContext context) : IAnalyticsEventPublisher
{
    public bool ConfirmsDelivery => false;

    public Task PublishAsync(IntegrationEventEnvelope<AnalyticsUpdatedV1> envelope, CancellationToken cancellationToken)
    {
        Validate(envelope);
        var source = context.RequireSource(envelope.UserIdHash!);
        if (envelope.CausationId != source.EventId || envelope.CorrelationId != source.CorrelationId ||
            envelope.OccurredAtUtc != source.Payload.ChangedAtUtc)
        { throw new StorageConflictException(); }
        return context.WithRecordsAsync(envelope.UserIdHash!, async (records, _) =>
        {
            await PostgresAnalyticsReadModelStore.RequirePendingPublicationAsync(records, envelope.UserIdHash!, source.EventId, envelope.Payload.Currency);
            var existing = await records.GetAsync("analytics_outbox", "analytics", envelope.EventId);
            if (existing is not null)
            {
                var message = Decode(existing);
                // A replay after later projections must retain the first atomic publication, not recalculate its immutable facts.
                if (message.PublishedAtUtc is not null || message.Event.UserIdHash != envelope.UserIdHash ||
                    message.Event.CausationId != envelope.CausationId || message.Event.CorrelationId != envelope.CorrelationId ||
                    message.Event.OccurredAtUtc != envelope.OccurredAtUtc || message.Event.Payload.Currency != envelope.Payload.Currency)
                { throw new StorageConflictException(); }
                return true;
            }
            await records.InsertAsync("analytics_outbox", "analytics", envelope.EventId, Encode(new AnalyticsOutboxMessage(envelope, null)));
            return true;
        }, cancellationToken);
    }

    public Task<IReadOnlyList<AnalyticsOutboxMessage>> ReadPendingAsync(CancellationToken token = default) =>
        context.WithMaintenanceRecordsAsync<IReadOnlyList<AnalyticsOutboxMessage>>(async (records, _) =>
        {
            var durable = await records.GetAsync("outbox_scan", "analytics", "pending");
            var after = durable is null ? null : OwnedRecordCodec.Decode<ScanCursor>(1, durable).After;
            var pending = new List<AnalyticsOutboxMessage>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var wrapped = false;
            for (var page = 0; page < 10 && pending.Count < 25; page++)
            {
                var items = await records.ReadPageAsync("analytics_outbox", "analytics", after);
                if (items.Count == 0)
                {
                    after = null;
                    if (wrapped) { break; }
                    wrapped = true;
                    continue;
                }
                foreach (var item in items)
                {
                    var message = Decode(new DurableRecord(item.Revision, item.Payload));
                    after = item.Cursor;
                    if (message.PublishedAtUtc is null && seen.Add(message.Event.EventId)) { pending.Add(message); }
                    if (pending.Count == 25) { break; }
                }
            }
            await WriteCursorAsync(records, "pending", durable, after);
            return pending;
        }, token);

    public async Task MarkPublishedAsync(AnalyticsOutboxMessage expected, DateTimeOffset publishedAtUtc, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        Validate(expected.Event);
        if (publishedAtUtc == default || publishedAtUtc.Offset != TimeSpan.Zero || publishedAtUtc < expected.Event.OccurredAtUtc)
        { throw new ArgumentException("Invalid Analytics outbox acknowledgement."); }
        await context.WithRecordsAsync(expected.Event.UserIdHash!, async (records, _) =>
        {
            var durable = await records.GetAsync("analytics_outbox", "analytics", expected.Event.EventId) ?? throw new StorageUnavailableException();
            var message = Decode(durable);
            if (message.Event != expected.Event) { throw new StorageConflictException(); }
            await PostgresAnalyticsReadModelStore.CompletePublicationAsync(records, message.Event.UserIdHash!,
                message.Event.CausationId, message.Event.Payload.Currency);
            if (message.PublishedAtUtc is null)
            { await records.ReplaceAsync("analytics_outbox", "analytics", message.Event.EventId, durable.Revision, Encode(message with { PublishedAtUtc = publishedAtUtc })); }
            return true;
        }, token);
    }

    public Task<int> CleanupPublishedBatchAsync(DateTimeOffset nowUtc, CancellationToken token = default)
    {
        if (nowUtc == default || nowUtc.Offset != TimeSpan.Zero) { throw new ArgumentException("Invalid Analytics retention time."); }
        return context.WithMaintenanceRecordsAsync(async (records, _) =>
        {
            var scan = await records.GetAsync("outbox_scan", "analytics", "retention");
            var after = scan is null ? null : OwnedRecordCodec.Decode<ScanCursor>(1, scan).After;
            var page = await records.ReadPageAsync("analytics_outbox", "analytics", after, 25);
            var removed = 0;
            foreach (var item in page)
            {
                var message = Decode(new DurableRecord(item.Revision, item.Payload));
                if (message.PublishedAtUtc is not null && nowUtc - message.PublishedAtUtc.Value > TimeSpan.FromDays(30))
                {
                    await records.DeletePageItemAsync("analytics_outbox", "analytics", item.Cursor, item.Revision);
                    removed++;
                }
            }
            await WriteCursorAsync(records, "retention", scan, page.Count < 25 ? null : page[^1].Cursor);
            return removed;
        }, token);
    }

    private static Task<bool> WriteCursorAsync(OwnedRecordTransaction records, string id, DurableRecord? previous, string? after) =>
        previous is null ? records.InsertAsync("outbox_scan", "analytics", id, OwnedRecordCodec.Encode(1, new ScanCursor(after))) :
            records.ReplaceAsync("outbox_scan", "analytics", id, previous.Revision, OwnedRecordCodec.Encode(1, new ScanCursor(after)));

    public static void Validate(IntegrationEventEnvelope<AnalyticsUpdatedV1> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var payload = envelope.Payload ?? throw new ArgumentException("Missing Analytics publication.");
        AnalyticsProjectionCodec.Owner(envelope.UserIdHash!); AnalyticsProjectionCodec.Currency(payload.Currency);
        AnalyticsProjectionCodec.Identifier(envelope.CausationId); AnalyticsProjectionCodec.Identifier(envelope.CorrelationId);
        if (envelope.EventType != AnalyticsEventTypes.AnalyticsUpdated || envelope.SchemaVersion != AnalyticsEventTypes.SchemaVersion ||
            envelope.Producer != "financial-assistant-analytics-service" || envelope.EventId != StableId(envelope.CausationId, payload.Currency) ||
            envelope.OccurrenceId != envelope.EventId || envelope.OccurredAtUtc != payload.UpdatedAtUtc ||
            payload.UpdatedAtUtc == default || payload.UpdatedAtUtc.Offset != TimeSpan.Zero ||
            payload.ReferenceDate != DateOnly.FromDateTime(payload.UpdatedAtUtc.UtcDateTime) ||
            payload.MonthlyIncomeTotal < 0 || payload.MonthlyExpenseTotal < 0 || payload.DailyExpenseSpent < 0 || payload.DailyExpenseLimit is <= 0 ||
            payload.TopExpenseCategoryAmount < 0 || payload.TopExpenseCategoryAmount > payload.MonthlyExpenseTotal ||
            payload.UncategorizedExpenseTotal < 0 || payload.UncategorizedExpenseTotal > payload.MonthlyExpenseTotal || payload.DailyExpenseSpent > payload.MonthlyExpenseTotal ||
            (payload.TopExpenseCategoryId is null) != (payload.TopExpenseCategoryAmount == 0))
        { throw new ArgumentException("Invalid Analytics publication."); }
        if (payload.TopExpenseCategoryId is not null) { AnalyticsProjectionCodec.Identifier(payload.TopExpenseCategoryId); }
    }

    private static string StableId(string sourceEventId, string currency) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(string.Join('|', "analytics-updated", sourceEventId, currency)))).ToLowerInvariant()[..32];

    internal static async Task VerifySourceIntentsAsync(OwnedRecordTransaction records, IntegrationEventEnvelope<FinancialRecordChangedV1> source)
    {
        var currencies = await PostgresAnalyticsReadModelStore.ReadPendingCurrenciesAsync(records, source.UserIdHash!, source.EventId);
        foreach (var currency in currencies)
        {
            var durable = await records.GetAsync("analytics_outbox", "analytics", StableId(source.EventId, currency))
                ?? throw new StorageUnavailableException();
            var message = Decode(durable);
            if (message.PublishedAtUtc is not null || message.Event.UserIdHash != source.UserIdHash ||
                message.Event.CausationId != source.EventId || message.Event.CorrelationId != source.CorrelationId ||
                message.Event.Payload.Currency != currency || message.Event.OccurredAtUtc != source.Payload.ChangedAtUtc)
            { throw new StorageUnavailableException(); }
        }
    }

    private static byte[] Encode(AnalyticsOutboxMessage message)
    {
        Validate(message.Event);
        var e = message.Event;
        var p = e.Payload;
        return OwnedRecordCodec.Encode(1, new State(e.EventId, e.OccurrenceId, e.EventType, e.OccurredAtUtc, e.Producer,
            e.SchemaVersion, e.CorrelationId, e.CausationId, e.UserIdHash!, p.Currency, p.ReferenceDate, p.MonthlyIncomeTotal,
            p.MonthlyExpenseTotal, p.DailyExpenseLimit, p.DailyExpenseSpent, p.TopExpenseCategoryId, p.UpdatedAtUtc,
            p.TopExpenseCategoryAmount, p.UncategorizedExpenseTotal, message.PublishedAtUtc));
    }

    private static AnalyticsOutboxMessage Decode(DurableRecord durable)
    {
        try
        {
            var s = OwnedRecordCodec.Decode<State>(1, durable);
            var e = new IntegrationEventEnvelope<AnalyticsUpdatedV1>(s.EventId, s.OccurrenceId, s.EventType, s.OccurredAtUtc,
                s.Producer, s.SchemaVersion, s.CorrelationId, s.CausationId, s.Owner,
                new AnalyticsUpdatedV1(s.Currency, s.ReferenceDate, s.MonthlyIncome, s.MonthlyExpense, s.DailyExpenseLimit,
                    s.DailyExpense, s.TopExpenseCategoryId, s.UpdatedAtUtc, s.TopExpenseCategoryAmount, s.UncategorizedExpense));
            Validate(e);
            if (s.PublishedAtUtc is not null && (s.PublishedAtUtc.Value == default || s.PublishedAtUtc.Value.Offset != TimeSpan.Zero ||
                s.PublishedAtUtc < e.OccurredAtUtc) || durable.Revision != (s.PublishedAtUtc is null ? 0 : 1))
            { throw new StorageUnavailableException(); }
            return new AnalyticsOutboxMessage(e, s.PublishedAtUtc);
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    private sealed record ScanCursor([property: JsonRequired] string? After);
    private sealed record State([property: JsonRequired] string EventId, [property: JsonRequired] string OccurrenceId,
        [property: JsonRequired] string EventType, [property: JsonRequired] DateTimeOffset OccurredAtUtc,
        [property: JsonRequired] string Producer, [property: JsonRequired] int SchemaVersion,
        [property: JsonRequired] string CorrelationId, [property: JsonRequired] string CausationId,
        [property: JsonRequired] string Owner, [property: JsonRequired] string Currency,
        [property: JsonRequired] DateOnly ReferenceDate, [property: JsonRequired] decimal MonthlyIncome,
        [property: JsonRequired] decimal MonthlyExpense, [property: JsonRequired] decimal? DailyExpenseLimit,
        [property: JsonRequired] decimal DailyExpense, [property: JsonRequired] string? TopExpenseCategoryId,
        [property: JsonRequired] DateTimeOffset UpdatedAtUtc, [property: JsonRequired] decimal TopExpenseCategoryAmount,
        [property: JsonRequired] decimal UncategorizedExpense, [property: JsonRequired] DateTimeOffset? PublishedAtUtc);
}
