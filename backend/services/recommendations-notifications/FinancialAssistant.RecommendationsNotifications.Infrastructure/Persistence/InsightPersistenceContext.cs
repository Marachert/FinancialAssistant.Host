using System.Text.Json.Serialization;
using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure.Persistence;

public sealed record InsightOwnerPage(IReadOnlyList<string> Owners, string? NextCursor);

internal sealed record InsightSource(string Pipeline, string Id, string Owner, string Currency,
    string Correlation, DateTimeOffset AtUtc, string Fingerprint, object Value, NativeInsightOrder? Order = null);

public sealed class InsightPersistenceContext : IInsightProcessingTransaction
{
    private readonly OwnedPostgresStore database;
    private readonly AsyncLocal<Scope?> current = new();
    private readonly AsyncLocal<NativeInsightOrder?> nativeOrder = new();
    private readonly bool requireNativeOrder;

    public InsightPersistenceContext(OwnedPostgresStore database, bool requireNativeOrder = false)
    {
        if (database.ServiceId != "recommendations-notifications") { throw new ArgumentException("Insights require their independent storage role."); }
        this.database = database;
        this.requireNativeOrder = requireNativeOrder;
    }

    // Privileged native adapter boundary, never an HTTP setter or inferred timestamp order.
    public async Task<T> ExecuteNativeOrderedAsync<T>(string stream, long sequence,
        Func<CancellationToken, Task<T>> operation, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (current.Value is not null || nativeOrder.Value is not null || stream is not ("analytics" or "score") || sequence is <= 0 or long.MaxValue)
        { throw new ArgumentException("Invalid native insight order context."); }
        nativeOrder.Value = new NativeInsightOrder(stream, sequence);
        try { token.ThrowIfCancellationRequested(); return await operation(token); }
        finally { nativeOrder.Value = null; }
    }

    public Task<IReadOnlyList<FinancialRecommendation>> AnalyticsAsync(IntegrationEventEnvelope<AnalyticsUpdatedV1> source,
        Func<CancellationToken, Task<IReadOnlyList<FinancialRecommendation>>> operation, CancellationToken token)
    {
        Envelope(source, AnalyticsEventTypes.AnalyticsUpdated);
        var p = source.Payload;
        var facts = new AnalyticsInsightFacts(p.ReferenceDate, p.MonthlyIncomeTotal, p.MonthlyExpenseTotal,
            p.DailyExpenseLimit, p.DailyExpenseSpent, p.TopExpenseCategoryId, p.UpdatedAtUtc, p.TopExpenseCategoryAmount, p.UncategorizedExpenseTotal);
        InsightPersistenceValidation.Analytics(facts); InsightPersistenceValidation.Currency(p.Currency);
        var canonical = Copy(source, p with
        {
            MonthlyIncomeTotal = Decimal(p.MonthlyIncomeTotal),
            MonthlyExpenseTotal = Decimal(p.MonthlyExpenseTotal),
            DailyExpenseLimit = p.DailyExpenseLimit is { } limit ? Decimal(limit) : null,
            DailyExpenseSpent = Decimal(p.DailyExpenseSpent),
            TopExpenseCategoryAmount = Decimal(p.TopExpenseCategoryAmount),
            UncategorizedExpenseTotal = Decimal(p.UncategorizedExpenseTotal)
        });
        return RunRecommendations(new InsightSource("analytics", source.EventId, source.UserIdHash!, p.Currency,
            source.CorrelationId, source.OccurredAtUtc, InsightPersistenceValidation.Fingerprint(canonical), source), operation, token);
    }

    public Task<IReadOnlyList<FinancialRecommendation>> ScoreAsync(IntegrationEventEnvelope<ScoreCalculatedV1> source,
        Func<CancellationToken, Task<IReadOnlyList<FinancialRecommendation>>> operation, CancellationToken token)
    {
        Envelope(source, FinancialScoreEventTypes.ScoreCalculated);
        var p = source.Payload;
        InsightPersistenceValidation.Score(new ScoreInsightFacts(p.Score, p.FormulaVersion, p.CalculatedAtUtc));
        InsightPersistenceValidation.Currency(p.Currency); InsightPersistenceValidation.Id(p.CalculationId);
        if (p.Factors is null || p.Factors.Count != 5 || p.Factors.Select(f => f.Code).Distinct().Count() != 5)
        { throw new ArgumentException("Invalid Score factor envelope."); }
        var known = new[] { "budget_usage", "spending_trend", "income_consistency", "data_completeness", "penalty_cap" };
        foreach (var f in p.Factors)
        {
            if (!known.Contains(f.Code, StringComparer.Ordinal) || f.Contribution is < -100 or > 100)
            { throw new ArgumentException("Invalid deterministic Score factor."); }
        }
        var canonical = Copy(source, p with { Factors = p.Factors.Select(f => f with { Contribution = Decimal(f.Contribution) }).ToArray() });
        return RunRecommendations(new InsightSource("score", source.EventId, source.UserIdHash!, p.Currency,
            source.CorrelationId, source.OccurredAtUtc, InsightPersistenceValidation.Fingerprint(canonical), source), operation, token);
    }

    public Task<IReadOnlyList<PreparedNotification>> PrepareAsync(IntegrationEventEnvelope<RecommendationGeneratedV1> source,
        Func<CancellationToken, Task<IReadOnlyList<PreparedNotification>>> operation, CancellationToken token)
    {
        Envelope(source, RecommendationEventTypes.RecommendationGenerated);
        var p = source.Payload;
        if (p.Facts is null) { throw new ArgumentException("Recommendation facts are required."); }
        InsightPersistenceValidation.Recommendation(new FinancialRecommendation(p.RecommendationId, source.UserIdHash!, p.Currency,
            p.Code, p.Severity, p.Title, p.Body, p.Facts.Select(f => new RecommendationFact(f.Code, f.Value)).ToArray(), source.EventId,
            p.GeneratedAtUtc, RecommendationStatuses.Active, p.GeneratedAtUtc));
        var canonical = Copy(source, p with { Facts = p.Facts.Select(f => f with { Value = Decimal(f.Value) }).ToArray() });
        return RunNotifications(new InsightSource("prepare", source.EventId, source.UserIdHash!, p.Currency,
            source.CorrelationId, source.OccurredAtUtc, InsightPersistenceValidation.Fingerprint(canonical), source), operation, token);
    }

    public Task<IReadOnlyList<PreparedNotification>> TriggerAsync(NotificationTriggerFacts source,
        Func<CancellationToken, Task<IReadOnlyList<PreparedNotification>>> operation, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(source);
        InsightPersistenceValidation.Owner(source.UserIdHash); InsightPersistenceValidation.Currency(source.Currency);
        InsightPersistenceValidation.Id(source.SourceEventId); InsightPersistenceValidation.Id(source.CorrelationId); InsightPersistenceValidation.Utc(source.OccurredAtUtc);
        _ = new NotificationTriggerEvaluator().Evaluate(source);
        var canonical = source with { BudgetLimit = source.BudgetLimit is { } limit ? Decimal(limit) : null, ConfirmedBudgetSpend = Decimal(source.ConfirmedBudgetSpend) };
        return RunNotifications(new InsightSource("trigger", source.SourceEventId, source.UserIdHash, source.Currency,
            source.CorrelationId, source.OccurredAtUtc, InsightPersistenceValidation.Fingerprint(canonical), source), operation, token);
    }

    private Task<IReadOnlyList<FinancialRecommendation>> RunRecommendations(InsightSource source,
        Func<CancellationToken, Task<IReadOnlyList<FinancialRecommendation>>> operation, CancellationToken token) =>
        RunSourceAsync(source, operation, r => r.RecommendationId,
            (s, ids) => ids.Select(id => s.Recommendations.Single(r => r.Id == id).ToDomain(s.Owner)).ToArray(), token);
    private Task<IReadOnlyList<PreparedNotification>> RunNotifications(InsightSource source,
        Func<CancellationToken, Task<IReadOnlyList<PreparedNotification>>> operation, CancellationToken token) =>
        RunSourceAsync(source, operation, n => n.NotificationId,
            (s, ids) => ids.Select(id => s.Notifications.Single(n => n.Id == id).ToDomain(s.Owner)).ToArray(), token);

    private Task<IReadOnlyList<T>> RunSourceAsync<T>(InsightSource source,
        Func<CancellationToken, Task<IReadOnlyList<T>>> operation, Func<T, string> identify,
        Func<InsightState, string[], IReadOnlyList<T>> replay, CancellationToken token)
    {
        if (nativeOrder.Value is { } order)
        {
            if (source.Pipeline != "trigger" && source.Pipeline != order.Stream)
            { throw new ArgumentException("Native insight order does not match its source pipeline."); }
            source = source with { Order = order, Fingerprint = InsightPersistenceValidation.Fingerprint(new { source.Fingerprint, order.Stream, order.Sequence }) };
        }
        else if (requireNativeOrder && source.Pipeline is "analytics" or "score" or "trigger")
        { throw new StorageUnavailableException(); }
        return RunOwnerAsync(source.Owner, source, async deadline =>
        {
            var scope = RequireScope();
            var existing = scope.State.Receipts.SingleOrDefault(r => r.Pipeline == source.Pipeline && r.SourceId == source.Id);
            if (existing is not null)
            {
                if (existing.Fingerprint != source.Fingerprint) { throw new StorageConflictException(); }
                return replay(scope.State, existing.Results);
            }
            if (source.Order is not null && !await NativeInsightOrderAdmission.AdmitAsync(scope.Records, source))
            {
                scope.State.Receipts.Add(new InsightReceiptState(source.Pipeline, source.Id, source.Fingerprint, []));
                return Array.Empty<T>();
            }
            if (requireNativeOrder && source.Pipeline == "prepare" && !CanPrepareNative(scope.State, source))
            {
                scope.State.Receipts.Add(new InsightReceiptState(source.Pipeline, source.Id, source.Fingerprint, []));
                return Array.Empty<T>();
            }
            var result = await operation(deadline);
            var ids = result.Select(identify).ToArray();
            if (!scope.Completed || !ids.SequenceEqual(scope.Results, StringComparer.Ordinal)) { throw new StorageUnavailableException(); }
            var kind = source.Pipeline is "analytics" or "score" ? "recommendation" : "notification";
            if (ids.Any(id => !scope.State.Publications.Any(p => p.Kind == kind && p.ItemId == id && p.CausationId == source.Id && p.CorrelationId == source.Correlation)))
            { throw new StorageUnavailableException(); }
            scope.State.Receipts.Add(new InsightReceiptState(source.Pipeline, source.Id, source.Fingerprint, ids));
            return result;
        }, token);
    }

    internal Task<T> MutateAsync<T>(string owner, Func<CancellationToken, Task<T>> operation, CancellationToken token) =>
        RunOwnerAsync(owner, null, operation, token);

    private Task<T> RunOwnerAsync<T>(string owner, InsightSource? source, Func<CancellationToken, Task<T>> operation, CancellationToken token)
    {
        InsightPersistenceValidation.Owner(owner); ArgumentNullException.ThrowIfNull(operation);
        if (current.Value is not null) { throw new InvalidOperationException("Insight transactions cannot nest."); }
        var locks = source is null ? new[] { "owner:" + owner } : new[] { "owner:" + owner, "source:" + source.Pipeline + ":" + source.Id };
        return database.ExecuteAsync(locks, async (records, deadline) =>
        {
            if (source is not null) { await ClaimAsync(records, source.Pipeline + ":" + source.Id, owner, source.Fingerprint); }
            var durable = await records.GetAsync("insight_state", owner, "v1");
            var scope = new Scope(durable is null ? InsightState.Empty(owner) : InsightPersistenceValidation.Decode(durable, owner), records, source);
            current.Value = scope;
            try
            {
                var result = await operation(deadline);
                var bytes = InsightPersistenceValidation.Encode(scope.State);
                if (durable is null)
                {
                    await records.InsertAsync("insight_state", owner, "v1", bytes);
                    await records.InsertAsync("insight_owners", "recommendations-notifications", owner, OwnedRecordCodec.Encode(1, new OwnerMarker(owner)));
                }
                else if (!bytes.AsSpan().SequenceEqual(durable.Payload)) { await records.ReplaceAsync("insight_state", owner, "v1", durable.Revision, bytes); }
                return result;
            }
            finally { current.Value = null; }
        }, token);
    }

    internal Task<T> ReadAsync<T>(string owner, Func<InsightState, T> read, CancellationToken token)
    {
        InsightPersistenceValidation.Owner(owner);
        if (current.Value is { } scope)
        {
            token.ThrowIfCancellationRequested();
            if (scope.State.Owner != owner) { throw new StorageUnavailableException(); }
            return Task.FromResult(read(scope.State));
        }
        return database.ExecuteAsync(new[] { "owner:" + owner }, async (records, _) =>
        {
            var durable = await records.GetAsync("insight_state", owner, "v1");
            return read(durable is null ? InsightState.Empty(owner) : InsightPersistenceValidation.Decode(durable, owner));
        }, token);
    }

    internal Task<T> WithRecordsAsync<T>(string owner, Func<OwnedRecordTransaction, CancellationToken, Task<T>> operation, CancellationToken token)
    {
        InsightPersistenceValidation.Owner(owner);
        if (current.Value is { } scope)
        {
            if (scope.State.Owner != owner) { throw new StorageUnavailableException(); }
            return operation(scope.Records, token);
        }
        return database.ExecuteAsync(new[] { "owner:" + owner }, operation, token);
    }

    internal Task<T> WithMaintenanceRecordsAsync<T>(Func<OwnedRecordTransaction, CancellationToken, Task<T>> operation, CancellationToken token)
    {
        if (current.Value is not null || nativeOrder.Value is not null) { throw new InvalidOperationException("Insight maintenance cannot nest."); }
        return database.ExecuteAsync(new[] { "insight:maintenance" }, operation, token);
    }

    internal static string DecodeOwnerMarker(DurableRecord record)
    {
        var owner = OwnedRecordCodec.Decode<OwnerMarker>(1, record).Owner;
        InsightPersistenceValidation.Owner(owner);
        if (record.Revision != 0) { throw new StorageUnavailableException(); }
        return owner;
    }

    private static bool CanPrepareNative(InsightState state, InsightSource source)
    {
        if (source.Value is not IntegrationEventEnvelope<RecommendationGeneratedV1> envelope) { throw new StorageConflictException(); }
        var recommendation = state.Recommendations.SingleOrDefault(r => r.Id == envelope.Payload.RecommendationId) ?? throw new StorageUnavailableException();
        var publication = state.Publications.SingleOrDefault(p => p.Kind == "recommendation" && p.ItemId == recommendation.Id)
            ?? throw new StorageUnavailableException();
        var expected = PostgresInsightEventPublisher.MapRecommendation(state.Owner, recommendation, publication);
        var canonical = Copy(expected, expected.Payload with { Facts = expected.Payload.Facts.Select(f => f with { Value = Decimal(f.Value) }).ToArray() });
        if (InsightPersistenceValidation.Fingerprint(canonical) != source.Fingerprint) { throw new StorageConflictException(); }
        return !RecommendationStatuses.IsTerminal(recommendation.Status);
    }

    internal InsightState State(string? owner = null)
    {
        var scope = RequireScope();
        if (owner is not null && scope.State.Owner != owner) { throw new StorageUnavailableException(); }
        return scope.State;
    }
    internal InsightSource Source => RequireScope().Source ?? throw new StorageUnavailableException();
    internal bool HasSource => current.Value?.Source is not null;
    internal RecommendationProfileSettings? Profile { get => RequireScope().Profile; set => RequireScope().Profile = value; }
    internal void SetPreferences(InsightPreferenceState p) => RequireScope().State = State() with { Preferences = p };
    internal void Results(string[] ids, bool completed) { RequireScope().Results = ids; RequireScope().Completed = completed; }
    internal void Complete() => RequireScope().Completed = true;
    internal void Applied() => RequireScope().Applied = true;
    internal bool WasApplied => RequireScope().Applied;
    internal Task ClaimItemAsync(string kind, string id) => ClaimAsync(RequireScope().Records, kind + ":" + id, State().Owner, "item");

    public Task<InsightOwnerPage> ReadOwnerPageAsync(string? afterCursor = null, int pageSize = 25, CancellationToken token = default)
    {
        if (current.Value is not null || pageSize is < 1 or > 25) { throw new ArgumentException("Invalid insight owner scan context/bound."); }
        return database.ExecuteAsync<InsightOwnerPage>([], async (records, _) =>
        {
            var page = await records.ReadPageAsync("insight_owners", "recommendations-notifications", afterCursor, pageSize);
            var owners = page.Select(p => OwnedRecordCodec.Decode<OwnerMarker>(1, new DurableRecord(p.Revision, p.Payload)).Owner).ToArray();
            foreach (var owner in owners) { InsightPersistenceValidation.Owner(owner); }
            if (page.Any(p => p.Revision != 0) || owners.Distinct().Count() != owners.Length) { throw new StorageUnavailableException(); }
            return new InsightOwnerPage(owners, page.Count < pageSize ? null : page[^1].Cursor);
        }, token);
    }

    private Scope RequireScope() => current.Value ?? throw new StorageUnavailableException();
    private static async Task ClaimAsync(OwnedRecordTransaction records, string id, string owner, string fingerprint)
    {
        var existing = await records.GetAsync("insight_claims", "recommendations-notifications", id);
        var expected = new Claim(owner, fingerprint);
        if (existing is null) { await records.InsertAsync("insight_claims", "recommendations-notifications", id, OwnedRecordCodec.Encode(1, expected)); }
        else if (existing.Revision != 0 || OwnedRecordCodec.Decode<Claim>(1, existing) != expected) { throw new StorageConflictException(); }
    }
    private static void Envelope<T>(IntegrationEventEnvelope<T> e, string type)
    {
        ArgumentNullException.ThrowIfNull(e); InsightPersistenceValidation.Owner(e.UserIdHash!); InsightPersistenceValidation.Utc(e.OccurredAtUtc);
        foreach (var id in new[] { e.EventId, e.OccurrenceId, e.Producer, e.CorrelationId, e.CausationId }) { InsightPersistenceValidation.Id(id); }
        if (e.EventType != type || e.SchemaVersion != 1 || e.Payload is null) { throw new ArgumentException("Invalid insight source envelope."); }
    }
    private static decimal Decimal(decimal value) => OwnedRecordCodec.CanonicalDecimal(value);
    private static IntegrationEventEnvelope<T> Copy<T>(IntegrationEventEnvelope<T> e, T payload) =>
        new(e.EventId, e.OccurrenceId, e.EventType, e.OccurredAtUtc, e.Producer, e.SchemaVersion, e.CorrelationId, e.CausationId, e.UserIdHash, payload);
    private sealed class Scope(InsightState state, OwnedRecordTransaction records, InsightSource? source)
    {
        public InsightState State { get; set; } = state;
        public OwnedRecordTransaction Records { get; } = records;
        public InsightSource? Source { get; } = source;
        public string[] Results { get; set; } = [];
        public bool Completed { get; set; }
        public bool Applied { get; set; }
        public RecommendationProfileSettings? Profile { get; set; }
    }
    private sealed record Claim([property: JsonRequired] string Owner, [property: JsonRequired] string Fingerprint);
    private sealed record OwnerMarker([property: JsonRequired] string Owner);
}
