using System.Security.Cryptography;
using System.Text.Json.Serialization;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Domain;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Analytics.Infrastructure.Persistence;

public sealed class AnalyticsPersistenceContext : IAnalyticsProjectionTransaction
{
    private readonly OwnedPostgresStore database;
    private readonly AsyncLocal<Scope?> current = new();

    public AnalyticsPersistenceContext(OwnedPostgresStore database)
    {
        if (database.ServiceId != "analytics") { throw new ArgumentException("Analytics requires its independent owned storage role."); }
        this.database = database;
    }

    public Task<T> WithRecordsAsync<T>(string owner, Func<OwnedRecordTransaction, CancellationToken, Task<T>> operation,
        CancellationToken token = default, IReadOnlyCollection<string>? additionalLocks = null)
    {
        AnalyticsProjectionCodec.Owner(owner);
        if (current.Value is { } scope)
        {
            if (scope.Owner != owner) { throw new StorageUnavailableException(); }
            return operation(scope.Records, token);
        }
        return database.ExecuteAsync(new[] { "owner:" + owner }.Concat(additionalLocks ?? Array.Empty<string>()).ToArray(), async (records, deadline) =>
        {
            current.Value = new Scope(owner, records, null);
            try { return await operation(records, deadline); }
            finally { current.Value = null; }
        }, token);
    }

    public async Task ExecuteAsync(IntegrationEventEnvelope<FinancialRecordChangedV1> source,
        Func<CancellationToken, Task> apply, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(apply);
        var projection = ReadSource(source);
        if (current.Value is not null) { throw new InvalidOperationException("Analytics source transactions cannot nest."); }
        await database.ExecuteAsync(new[] { "owner:" + projection.UserIdHash, "record:" + AnalyticsProjectionCodec.RecordKey(projection),
            "event:" + source.EventId }, async (records, deadline) =>
        {
            current.Value = new Scope(projection.UserIdHash, records, source);
            try
            {
                var canonical = new IntegrationEventEnvelope<FinancialRecordChangedV1>(source.EventId, source.OccurrenceId,
                    source.EventType, source.OccurredAtUtc, source.Producer, source.SchemaVersion, source.CorrelationId,
                    source.CausationId, source.UserIdHash, source.Payload with { Amount = OwnedRecordCodec.CanonicalDecimal(source.Payload.Amount) });
                var fingerprint = Convert.ToHexString(SHA256.HashData(OwnedRecordCodec.Encode(1, canonical)));
                var receipt = await records.GetAsync("source_receipts", projection.UserIdHash, source.EventId);
                if (receipt is not null)
                {
                    if (receipt.Revision != 0 || OwnedRecordCodec.Decode<SourceReceipt>(1, receipt) !=
                        new SourceReceipt(projection.UserIdHash, source.EventId, fingerprint))
                    { throw new StorageConflictException(); }
                    await PostgresAnalyticsEventPublisher.VerifySourceIntentsAsync(records, source);
                    return true;
                }
                else
                {
                    await records.InsertAsync("source_receipts", projection.UserIdHash, source.EventId,
                        OwnedRecordCodec.Encode(1, new SourceReceipt(projection.UserIdHash, source.EventId, fingerprint)));
                }
                await apply(deadline);
                await PostgresAnalyticsEventPublisher.VerifySourceIntentsAsync(records, source);
                return true;
            }
            finally { current.Value = null; }
        }, cancellationToken);
    }

    public IntegrationEventEnvelope<FinancialRecordChangedV1> RequireSource(string owner)
    {
        var scope = current.Value;
        if (scope?.Owner != owner || scope.Source is null) { throw new StorageUnavailableException(); }
        return scope.Source;
    }

    public Task<T> WithMaintenanceRecordsAsync<T>(Func<OwnedRecordTransaction, CancellationToken, Task<T>> operation,
        CancellationToken token = default)
    {
        if (current.Value is not null) { throw new InvalidOperationException("Analytics maintenance cannot nest in projection work."); }
        return database.ExecuteAsync(new[] { "outbox-maintenance" }, operation, token);
    }

    public void ValidateProjectionScope(AnalyticsRecordProjection projection)
    {
        var scope = current.Value;
        if (scope is not null && (scope.Owner != projection.UserIdHash || scope.Source is not null && ReadSource(scope.Source) != projection))
        { throw new StorageConflictException(); }
    }

    public static AnalyticsRecordProjection ReadSource(IntegrationEventEnvelope<FinancialRecordChangedV1> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var type = source.EventType switch
        {
            FinancialRecordEventTypes.IncomeCreated or FinancialRecordEventTypes.IncomeUpdated or
                FinancialRecordEventTypes.IncomeArchived or FinancialRecordEventTypes.IncomeRestored => "income",
            FinancialRecordEventTypes.ExpenseCreated or FinancialRecordEventTypes.ExpenseUpdated or
                FinancialRecordEventTypes.ExpenseArchived or FinancialRecordEventTypes.ExpenseRestored => "expense",
            _ => throw new ArgumentException("Invalid Analytics financial source type.")
        };
        if (source.SchemaVersion != FinancialRecordEventTypes.SchemaVersion || source.Payload is null ||
            source.Payload.Currency is null || source.Payload.ChangedAtUtc == default || source.Payload.ChangedAtUtc.Offset != TimeSpan.Zero ||
            source.Payload.Origin is not ("manual" or "confirmed_transaction") ||
            source.EventType.Contains(".archived.", StringComparison.Ordinal) && source.Payload.Status != "archived" ||
            source.EventType.Contains(".restored.", StringComparison.Ordinal) && source.Payload.Status != "active")
        { throw new ArgumentException("Invalid Analytics financial source."); }
        foreach (var value in new[] { source.EventId, source.OccurrenceId, source.Producer, source.CorrelationId, source.CausationId })
        { AnalyticsProjectionCodec.Identifier(value); }
        var payload = source.Payload;
        var projection = new AnalyticsRecordProjection(type, payload.RecordId, source.UserIdHash!, payload.Amount,
            payload.Currency.ToUpperInvariant(), string.IsNullOrWhiteSpace(payload.CategoryId) ? AnalyticsCategoryIds.Uncategorized : payload.CategoryId.Trim(),
            payload.Date, payload.Status, payload.Revision, payload.ChangedAtUtc.ToUniversalTime(), source.EventId);
        AnalyticsProjectionCodec.Validate(projection);
        return projection;
    }

    private sealed record Scope(string Owner, OwnedRecordTransaction Records, IntegrationEventEnvelope<FinancialRecordChangedV1>? Source);
    private sealed record SourceReceipt([property: JsonRequired] string Owner, [property: JsonRequired] string EventId,
        [property: JsonRequired] string Fingerprint);
}
