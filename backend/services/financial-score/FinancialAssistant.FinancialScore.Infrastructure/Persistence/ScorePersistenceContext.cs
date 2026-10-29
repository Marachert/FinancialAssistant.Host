using System.Text.Json.Serialization;
using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Domain;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.FinancialScore.Infrastructure.Persistence;

public sealed record ScoreOwnerPage(IReadOnlyList<string> Owners, string? NextCursor);

public sealed class ScorePersistenceContext : IFinancialScoreTransaction
{
    private readonly OwnedPostgresStore database;
    private readonly AsyncLocal<Scope?> current = new();

    public ScorePersistenceContext(OwnedPostgresStore database)
    {
        if (database.ServiceId != "financial-score") { throw new ArgumentException("Score requires its independent storage role."); }
        this.database = database;
    }

    public Task<T> ExecuteAsync<T>(IntegrationEventEnvelope<FinancialRecordChangedV1> source,
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        var projection = ReadSource(source);
        var canonical = new IntegrationEventEnvelope<FinancialRecordChangedV1>(source.EventId, source.OccurrenceId,
            source.EventType, source.OccurredAtUtc, source.Producer, source.SchemaVersion, source.CorrelationId,
            source.CausationId, source.UserIdHash, source.Payload with { Amount = OwnedRecordCodec.CanonicalDecimal(source.Payload.Amount) });
        return RunAsync(projection.UserIdHash, source, ScorePersistenceValidation.Fingerprint(canonical), operation, cancellationToken);
    }

    public Task<T> ExecuteOwnerAsync<T>(string owner, Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken) => RunAsync(owner, null, null, operation, cancellationToken);

    private Task<T> RunAsync<T>(string owner, IntegrationEventEnvelope<FinancialRecordChangedV1>? source,
        string? fingerprint, Func<CancellationToken, Task<T>> operation, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ScorePersistenceValidation.Owner(owner);
        if (current.Value is not null) { throw new InvalidOperationException("Score transactions cannot nest."); }
        var locks = new List<string> { "owner:" + owner };
        if (source is not null) { locks.Add("source:" + source.EventId); locks.Add("record:" + ReadSource(source).RecordType + ":" + source.Payload.RecordId); }
        return database.ExecuteAsync(locks, async (records, deadline) =>
        {
            var durable = await records.GetAsync("score_state", owner, "v1");
            var state = durable is null ? ScoreState.Empty(owner) : ScorePersistenceValidation.Decode(durable, owner);
            var newSource = false;
            if (source is not null)
            {
                await ClaimAsync(records, "event:" + source.EventId, owner, fingerprint!);
                await ClaimAsync(records, "record:" + ReadSource(source).RecordType + ":" + source.Payload.RecordId, owner, "record");
                var receipt = state.Sources.SingleOrDefault(s => s.EventId == source.EventId);
                if (receipt is not null && receipt.Fingerprint != fingerprint) { throw new StorageConflictException(); }
                if (receipt is null) { state.Sources.Add(new ScoreSourceState(source.EventId, fingerprint!)); newSource = true; }
            }
            var scope = new Scope(state, source, records);
            current.Value = scope;
            try
            {
                var result = await operation(deadline);
                if (newSource && scope.ExpectedCurrencies is null) { throw new StorageUnavailableException(); }
                if (scope.ExpectedCurrencies is { } expected && (state.Calculations.Count(c => c.Source == source!.EventId) != expected.Length ||
                    expected.Any(currency => !state.Calculations.Any(c => c.Source == source!.EventId && c.Currency == currency))))
                { throw new StorageUnavailableException(); }
                if (source is not null && state.Calculations.Where(c => c.Source == source.EventId)
                    .Any(c => !state.Publications.Any(p => p.CalculationId == c.Id)))
                { throw new StorageUnavailableException(); }
                var bytes = ScorePersistenceValidation.Encode(state);
                if (durable is null)
                {
                    await records.InsertAsync("score_state", owner, "v1", bytes);
                    await records.InsertAsync("score_owners", "financial-score", owner, OwnedRecordCodec.Encode(1, new OwnerMarker(owner)));
                }
                else if (!bytes.AsSpan().SequenceEqual(durable.Payload)) { await records.ReplaceAsync("score_state", owner, "v1", durable.Revision, bytes); }
                return result;
            }
            finally { current.Value = null; }
        }, token);
    }

    internal Task<T> ReadAsync<T>(string owner, Func<ScoreState, T> read, CancellationToken token)
    {
        ScorePersistenceValidation.Owner(owner);
        if (current.Value is { } scope)
        {
            if (scope.State.Owner != owner) { throw new StorageUnavailableException(); }
            token.ThrowIfCancellationRequested();
            return Task.FromResult(read(scope.State));
        }
        return database.ExecuteAsync(new[] { "owner:" + owner }, async (records, _) =>
        {
            var durable = await records.GetAsync("score_state", owner, "v1");
            return read(durable is null ? ScoreState.Empty(owner) : ScorePersistenceValidation.Decode(durable, owner));
        }, token);
    }

    internal ScoreState RequireState(string? owner = null)
    {
        var state = current.Value?.State ?? throw new StorageUnavailableException();
        if (owner is not null && state.Owner != owner) { throw new StorageUnavailableException(); }
        return state;
    }

    internal IntegrationEventEnvelope<FinancialRecordChangedV1> RequireSource() =>
        current.Value?.Source ?? throw new StorageUnavailableException();

    internal IntegrationEventEnvelope<FinancialRecordChangedV1>? CurrentSource => current.Value?.Source;

    public Task<ScoreOwnerPage> ReadOwnerPageAsync(string? afterCursor = null, int pageSize = 25, CancellationToken token = default)
    {
        if (current.Value is not null || pageSize is < 1 or > 25) { throw new ArgumentException("Invalid Score owner scan context/bound."); }
        return database.ExecuteAsync<ScoreOwnerPage>([], async (records, _) =>
        {
            var page = await records.ReadPageAsync("score_owners", "financial-score", afterCursor, pageSize);
            var owners = new List<string>();
            foreach (var item in page)
            {
                var owner = OwnedRecordCodec.Decode<OwnerMarker>(1, new DurableRecord(item.Revision, item.Payload)).Owner;
                ScorePersistenceValidation.Owner(owner);
                if (item.Revision != 0 || owners.Contains(owner, StringComparer.Ordinal)) { throw new StorageUnavailableException(); }
                owners.Add(owner);
            }
            return new ScoreOwnerPage(owners, page.Count < pageSize ? null : page[^1].Cursor);
        }, token);
    }

    internal Task<T> WithRecordsAsync<T>(string owner, Func<OwnedRecordTransaction, CancellationToken, Task<T>> operation, CancellationToken token)
    {
        ScorePersistenceValidation.Owner(owner);
        if (current.Value is { } scope)
        {
            if (scope.State.Owner != owner) { throw new StorageUnavailableException(); }
            return operation(scope.Records, token);
        }
        return database.ExecuteAsync(new[] { "owner:" + owner }, operation, token);
    }

    internal void ExpectCurrencies(string[] currencies) =>
        (current.Value ?? throw new StorageUnavailableException()).ExpectedCurrencies = currencies;

    private static async Task ClaimAsync(OwnedRecordTransaction records, string id, string owner, string fingerprint)
    {
        var previous = await records.GetAsync("score_claims", "financial-score", id);
        var expected = new Claim(owner, fingerprint);
        if (previous is null) { await records.InsertAsync("score_claims", "financial-score", id, OwnedRecordCodec.Encode(1, expected)); }
        else if (previous.Revision != 0 || OwnedRecordCodec.Decode<Claim>(1, previous) != expected) { throw new StorageConflictException(); }
    }

    public static FinancialScoreRecordProjection ReadSource(IntegrationEventEnvelope<FinancialRecordChangedV1> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var type = source.EventType switch
        {
            FinancialRecordEventTypes.IncomeCreated or FinancialRecordEventTypes.IncomeUpdated or
                FinancialRecordEventTypes.IncomeArchived or FinancialRecordEventTypes.IncomeRestored => "income",
            FinancialRecordEventTypes.ExpenseCreated or FinancialRecordEventTypes.ExpenseUpdated or
                FinancialRecordEventTypes.ExpenseArchived or FinancialRecordEventTypes.ExpenseRestored => "expense",
            _ => throw new ArgumentException("Invalid Score financial source type.")
        };
        if (source.SchemaVersion != FinancialRecordEventTypes.SchemaVersion || source.Payload is null ||
            source.Payload.Origin is not ("manual" or "confirmed_transaction") ||
            source.EventType.Contains(".archived.", StringComparison.Ordinal) && source.Payload.Status != "archived" ||
            source.EventType.Contains(".restored.", StringComparison.Ordinal) && source.Payload.Status != "active")
        { throw new ArgumentException("Invalid Score financial source."); }
        foreach (var id in new[] { source.EventId, source.OccurrenceId, source.Producer, source.CorrelationId, source.CausationId })
        { ScorePersistenceValidation.Identifier(id); }
        if (source.Payload.CategoryId is not null) { ScorePersistenceValidation.Identifier(source.Payload.CategoryId); }
        ScorePersistenceValidation.Utc(source.OccurredAtUtc);
        var p = source.Payload;
        var projection = new FinancialScoreRecordProjection(type, p.RecordId, source.UserIdHash!, p.Amount,
            p.Currency, p.Date, p.Status, p.Revision, p.ChangedAtUtc, source.EventId);
        ScorePersistenceValidation.Projection(projection);
        return projection;
    }

    private sealed class Scope(ScoreState state, IntegrationEventEnvelope<FinancialRecordChangedV1>? source, OwnedRecordTransaction records)
    {
        public ScoreState State { get; } = state;
        public IntegrationEventEnvelope<FinancialRecordChangedV1>? Source { get; } = source;
        public OwnedRecordTransaction Records { get; } = records;
        public string[]? ExpectedCurrencies { get; set; }
    }
    private sealed record Claim([property: JsonRequired] string Owner, [property: JsonRequired] string Fingerprint);
    private sealed record OwnerMarker([property: JsonRequired] string Owner);
}
