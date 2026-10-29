using FinancialAssistant.Audit.Application;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Audit.Infrastructure.Persistence;

public sealed record AuditCleanupResult(int Scanned, int Deleted, string? NextCursor);

public sealed class PostgresAuditRecordStore : IAuditRecordStore, IAuditEventTransaction
{
    private const string Owner = "audit";
    private readonly OwnedPostgresStore database;
    private readonly AuditPolicy policy;
    private readonly Dictionary<string, int> configuredDays;
    private readonly TimeProvider timeProvider;
    private readonly AsyncLocal<Scope?> current = new();

    public PostgresAuditRecordStore(OwnedPostgresStore database, AuditPolicy policy,
        IReadOnlyDictionary<string, int> retentionDays, TimeProvider timeProvider)
    {
        if (database.ServiceId != "audit") { throw new ArgumentException("Audit requires its own storage identity."); }
        this.database = database; this.policy = policy; this.timeProvider = timeProvider;
        configuredDays = retentionDays.ToDictionary(p => p.Key.Trim().ToLowerInvariant(), p => p.Value, StringComparer.Ordinal);
        ValidateLifecycle(new AuditLifecycleState(configuredDays, null));
    }

    public Task<string> ExecuteAsync(IntegrationEventEnvelope<AuditEventV1> source,
        Func<CancellationToken, Task<string>> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        AuditPersistenceValidation.Source(source, policy, timeProvider.GetUtcNow());
        AuditPolicy.EnsureSafeIdentifier(source.Producer, nameof(source));
        if (current.Value is not null) { throw new InvalidOperationException("Audit transactions cannot nest."); }
        var fingerprint = AuditPersistenceValidation.Fingerprint(source);
        return database.ExecuteAsync(new[] { "audit-append-retention" }, async (records, deadline) =>
        {
            var lifecycle = await LifecycleAsync(records);
            var existing = await records.GetAsync("audit_receipts", Owner, source.EventId);
            if (existing is not null)
            {
                var receipt = Receipt(existing, source.EventId);
                if (receipt.Fingerprint != fingerprint) { throw new StorageConflictException(); }
                return receipt.AuditId;
            }
            var scope = new Scope(records, source, lifecycle.State.MinimumDays);
            current.Value = scope;
            try
            {
                var result = await operation(deadline);
                if (!scope.Appended || result != AuditPersistenceValidation.AuditId(source.EventId))
                { throw new StorageUnavailableException(); }
                await records.InsertAsync("audit_receipts", Owner, source.EventId,
                    OwnedRecordCodec.Encode(1, new AuditReceiptState(fingerprint, result)));
                return result;
            }
            finally { current.Value = null; }
        }, cancellationToken);
    }

    public async Task<AuditAppendResult> AppendAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); ArgumentNullException.ThrowIfNull(record);
        var scope = current.Value ?? throw new StorageUnavailableException();
        if (scope.Appended) { throw new StorageConflictException(); }
        var e = scope.Source; var p = e.Payload;
        var expected = new AuditRecord(AuditPersistenceValidation.AuditId(e.EventId), e.EventId, e.OccurredAtUtc,
            record.RecordedAtUtc, e.Producer.Trim().ToLowerInvariant(), e.CorrelationId, e.CausationId, e.UserIdHash,
            p.Domain.Trim().ToLowerInvariant(), p.Action.Trim().ToLowerInvariant(), p.Outcome.Trim().ToLowerInvariant(),
            p.ResourceType.Trim().ToLowerInvariant(), p.FailureCategory?.Trim().ToLowerInvariant(),
            p.RetentionClass.Trim().ToLowerInvariant(), policy.ExpiresAt(e.OccurredAtUtc, p.RetentionClass),
            AuditPolicy.NormalizeActorType(p.ActorType), p.ActorIdHash);
        if (record != expected) { throw new StorageConflictException(); }
        var expiry = EffectiveExpiry(record.OccurredAtUtc, record.ExpiresAtUtc, record.RetentionClass, scope.MinimumDays);
        var value = AuditEntryState.From(record with { ExpiresAtUtc = expiry }, e.OccurrenceId, p.ActorType is null && p.ActorIdHash is null);
        AuditPersistenceValidation.Entry(value, policy);
        if (expiry > timeProvider.GetUtcNow())
        {
            var index = await ReadIndexAsync(scope.Records, record.CorrelationId);
            if (index.Count >= 1000) { throw new StorageUnavailableException(); }
            await scope.Records.InsertAsync("audit_entries", Owner, value.Id, OwnedRecordCodec.Encode(1, value));
            await scope.Records.InsertAsync("audit_correlation", record.CorrelationId, value.Id,
                OwnedRecordCodec.Encode(1, new AuditIndexState(value.Id)));
        }
        scope.Appended = true;
        return AuditAppendResult.Appended;
    }

    public Task<IReadOnlyList<AuditRecord>> FindByCorrelationAsync(string correlationId,
        DateTimeOffset asOfUtc, CancellationToken cancellationToken)
    {
        AuditPolicy.EnsureSafeEnvelopeIdentifier(correlationId, nameof(correlationId)); AuditPersistenceValidation.Utc(asOfUtc);
        if (current.Value is not null) { throw new InvalidOperationException("Audit queries cannot nest in append."); }
        return database.ExecuteAsync<IReadOnlyList<AuditRecord>>(new[] { "audit-append-retention" }, async (records, _) =>
        {
            var lifecycle = await LifecycleAsync(records);
            var now = timeProvider.GetUtcNow(); if (asOfUtc > now) { now = asOfUtc; }
            var result = new List<AuditRecord>();
            foreach (var id in await ReadIndexAsync(records, correlationId))
            {
                var durable = await records.GetAsync("audit_entries", Owner, id) ?? throw new StorageUnavailableException();
                var value = AuditPersistenceValidation.Decode(durable, policy);
                if (value.Id != id || value.Correlation != correlationId) { throw new StorageUnavailableException(); }
                var expires = EffectiveExpiry(value.OccurredAtUtc, value.ExpiresAtUtc, value.Retention, lifecycle.State.MinimumDays);
                if (expires > now) { result.Add(value.ToRecord() with { ExpiresAtUtc = expires }); }
            }
            return result.OrderBy(r => r.OccurredAtUtc).ThenBy(r => r.AuditId, StringComparer.Ordinal).ToArray();
        }, cancellationToken);
    }

    // Internal lifecycle operation, never an operator update/delete endpoint.
    public Task<AuditCleanupResult> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        if (current.Value is not null) { throw new InvalidOperationException("Audit cleanup cannot nest in append."); }
        return database.ExecuteAsync(new[] { "audit-append-retention" }, async (records, _) =>
        {
            var lifecycle = await LifecycleAsync(records);
            var page = await records.ReadPageAsync("audit_entries", Owner, lifecycle.State.CleanupCursor, 25);
            var now = timeProvider.GetUtcNow(); var deleted = 0;
            foreach (var item in page)
            {
                var value = AuditPersistenceValidation.Decode(new DurableRecord(item.Revision, item.Payload), policy);
                if (EffectiveExpiry(value.OccurredAtUtc, value.ExpiresAtUtc, value.Retention, lifecycle.State.MinimumDays) > now) { continue; }
                var index = await records.GetAsync("audit_correlation", value.Correlation, value.Id) ?? throw new StorageUnavailableException();
                if (Index(index) != value.Id) { throw new StorageUnavailableException(); }
                await records.DeleteAsync("audit_entries", Owner, value.Id, 0);
                await records.DeleteAsync("audit_correlation", value.Correlation, value.Id, 0);
                deleted++;
            }
            var next = page.Count == 25 ? page[^1].Cursor : null;
            await records.ReplaceAsync("audit_lifecycle", Owner, "v1", lifecycle.Revision,
                OwnedRecordCodec.Encode(1, lifecycle.State with { CleanupCursor = next }));
            return new AuditCleanupResult(page.Count, deleted, next);
        }, cancellationToken);
    }

    private async Task<(AuditLifecycleState State, long Revision)> LifecycleAsync(OwnedRecordTransaction records)
    {
        var durable = await records.GetAsync("audit_lifecycle", Owner, "v1");
        AuditLifecycleState state;
        try { state = durable is null ? new AuditLifecycleState(new(StringComparer.Ordinal), null) : OwnedRecordCodec.Decode<AuditLifecycleState>(1, durable); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { throw new StorageUnavailableException(); }
        if (durable is not null) { ValidateLifecycle(state); }
        var days = new Dictionary<string, int>(state.MinimumDays, StringComparer.Ordinal);
        foreach (var pair in configuredDays) { days[pair.Key] = days.TryGetValue(pair.Key, out var previous) ? Math.Min(previous, pair.Value) : pair.Value; }
        state = state with { MinimumDays = days }; ValidateLifecycle(state);
        var bytes = OwnedRecordCodec.Encode(1, state);
        if (durable is null) { await records.InsertAsync("audit_lifecycle", Owner, "v1", bytes); return (state, 0); }
        if (!bytes.AsSpan().SequenceEqual(durable.Payload))
        { await records.ReplaceAsync("audit_lifecycle", Owner, "v1", durable.Revision, bytes); return (state, durable.Revision + 1); }
        return (state, durable.Revision);
    }

    private static async Task<IReadOnlyList<string>> ReadIndexAsync(OwnedRecordTransaction records, string correlation)
    {
        var ids = new List<string>(); string? cursor = null;
        do
        {
            var page = await records.ReadPageAsync("audit_correlation", correlation, cursor, 100);
            ids.AddRange(page.Select(item => Index(new DurableRecord(item.Revision, item.Payload))));
            if (ids.Count > 1000 || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count) { throw new StorageUnavailableException(); }
            cursor = page.Count == 100 ? page[^1].Cursor : null;
        } while (cursor is not null);
        return ids;
    }

    private static string Index(DurableRecord record)
    {
        var index = OwnedRecordCodec.Decode<AuditIndexState>(1, record);
        if (record.Revision != 0 || index.AuditId is not { Length: 70 } || !index.AuditId.StartsWith("audit_", StringComparison.Ordinal) ||
            !index.AuditId[6..].All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f')) { throw new StorageUnavailableException(); }
        return index.AuditId;
    }
    private static AuditReceiptState Receipt(DurableRecord record, string source)
    {
        var receipt = OwnedRecordCodec.Decode<AuditReceiptState>(1, record);
        if (record.Revision != 0 || receipt.AuditId != AuditPersistenceValidation.AuditId(source) ||
            receipt.Fingerprint is not { Length: 64 } || !receipt.Fingerprint.All(char.IsAsciiHexDigit)) { throw new StorageUnavailableException(); }
        return receipt;
    }
    private static DateTimeOffset EffectiveExpiry(DateTimeOffset occurred, DateTimeOffset original,
        string retention, IReadOnlyDictionary<string, int> days)
    {
        if (!days.TryGetValue(retention, out var minimum)) { throw new StorageUnavailableException(); }
        var clamped = occurred.AddDays(minimum); return clamped < original ? clamped : original;
    }
    private static void ValidateLifecycle(AuditLifecycleState state)
    {
        if (state.MinimumDays is null || state.MinimumDays.Count is < 1 or > 16 ||
            state.MinimumDays.Any(p => p.Value is < 1 or > 3650 || p.Key != p.Key.Trim().ToLowerInvariant()) ||
            state.CleanupCursor is { } cursor && (cursor.Length != 64 || !cursor.All(char.IsAsciiHexDigit)))
        { throw new StorageUnavailableException(); }
        foreach (var key in state.MinimumDays.Keys)
        {
            try { AuditPolicy.EnsureSafeIdentifier(key, nameof(state)); }
            catch (ArgumentException) { throw new StorageUnavailableException(); }
        }
    }
    private sealed class Scope(OwnedRecordTransaction records, IntegrationEventEnvelope<AuditEventV1> source, Dictionary<string, int> days)
    {
        public OwnedRecordTransaction Records { get; } = records;
        public IntegrationEventEnvelope<AuditEventV1> Source { get; } = source;
        public Dictionary<string, int> MinimumDays { get; } = days;
        public bool Appended { get; set; }
    }
}
