using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.FinancialScore.Infrastructure.Persistence;

public sealed record ScoreOutboxMessage(IntegrationEventEnvelope<ScoreCalculatedV1> Event, DateTimeOffset? PublishedAtUtc, long? SourceSequence = null);

public sealed class PostgresFinancialScoreEventPublisher(ScorePersistenceContext context) : IFinancialScoreEventPublisher
{
    public async Task PublishAsync(IntegrationEventEnvelope<ScoreCalculatedV1> envelope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = context.RequireSource();
        var state = context.RequireState(envelope.UserIdHash);
        var calculation = state.Calculations.SingleOrDefault(c => c.Id == envelope.EventId) ?? throw new StorageUnavailableException();
        var candidate = new ScorePublicationState(calculation.Id, StableId("score-occurrence", source.OccurrenceId), source.CorrelationId, null);
        var expected = Map(state.Owner, calculation, candidate);
        if (source.EventId != calculation.Source || ScorePersistenceValidation.Fingerprint(expected) != ScorePersistenceValidation.Fingerprint(envelope))
        { throw new StorageConflictException(); }
        var existing = state.Publications.SingleOrDefault(p => p.CalculationId == calculation.Id);
        if (existing is null)
        {
            await context.WithRecordsAsync(state.Owner, (records, _) =>
                OwnedPublicationOrder.AllocateAsync(records, "score", state.Owner, calculation.Id), cancellationToken);
            state.Publications.Add(candidate);
        }
        else if (existing.OccurrenceId != candidate.OccurrenceId || existing.CorrelationId != candidate.CorrelationId) { throw new StorageConflictException(); }
    }

    public Task<IReadOnlyList<ScoreOutboxMessage>> ReadPendingAsync(string owner, CancellationToken token = default) =>
        context.ReadAsync<IReadOnlyList<ScoreOutboxMessage>>(owner, state => state.Publications.Where(p => p.PublishedAtUtc is null)
            .Take(25).Select(p => new ScoreOutboxMessage(Map(owner, state.Calculations.Single(c => c.Id == p.CalculationId), p), null)).ToArray(), token);

    public Task<IReadOnlyList<ScoreOutboxMessage>> ReadPendingBatchAsync(CancellationToken token = default) =>
        context.WithMaintenanceRecordsAsync<IReadOnlyList<ScoreOutboxMessage>>(async (records, _) =>
        {
            var cursor = await records.GetAsync("score_outbox_scan", "financial-score", "pending");
            var after = cursor is null ? null : OwnedRecordCodec.Decode<ScanCursor>(1, cursor).After;
            var result = new List<ScoreOutboxMessage>(); var seen = new HashSet<string>(StringComparer.Ordinal); var wrapped = false;
            for (var page = 0; page < 10 && result.Count < 25; page++)
            {
                var owners = await records.ReadPageAsync("score_owners", "financial-score", after, 25);
                if (owners.Count == 0)
                {
                    after = null; if (wrapped) { break; }
                    wrapped = true; continue;
                }
                foreach (var item in owners)
                {
                    after = item.Cursor;
                    var owner = ScorePersistenceContext.DecodeOwnerMarker(new DurableRecord(item.Revision, item.Payload));
                    if (!seen.Add(owner)) { continue; }
                    var durable = await records.GetAsync("score_state", owner, "v1");
                    if (durable is null) { continue; }
                    var state = ScorePersistenceValidation.Decode(durable, owner);
                    foreach (var publication in state.Publications.Where(p => p.PublishedAtUtc is null).Take(25 - result.Count))
                    {
                        var sequence = await OwnedPublicationOrder.ReadRequiredAsync(records, "score", owner, publication.CalculationId);
                        result.Add(new ScoreOutboxMessage(Map(owner, state.Calculations.Single(c => c.Id == publication.CalculationId), publication), null, sequence));
                    }
                    if (result.Count == 25) { break; }
                }
            }
            var encoded = OwnedRecordCodec.Encode(1, new ScanCursor(after));
            if (cursor is null) { await records.InsertAsync("score_outbox_scan", "financial-score", "pending", encoded); }
            else { await records.ReplaceAsync("score_outbox_scan", "financial-score", "pending", cursor.Revision, encoded); }
            return result;
        }, token);

    public Task<ScoreOutboxMessage?> GetAsync(string owner, string calculationId, CancellationToken token = default)
    {
        ScorePersistenceValidation.Identifier(calculationId);
        return context.ReadAsync(owner, state =>
        {
            var p = state.Publications.SingleOrDefault(p => p.CalculationId == calculationId);
            return p is null ? null : new ScoreOutboxMessage(Map(owner, state.Calculations.Single(c => c.Id == p.CalculationId), p), p.PublishedAtUtc);
        }, token);
    }

    public Task<bool> MarkPublishedAsync(ScoreOutboxMessage expected, DateTimeOffset publishedAtUtc, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(expected); ArgumentNullException.ThrowIfNull(expected.Event);
        ScorePersistenceValidation.Utc(publishedAtUtc);
        if (publishedAtUtc < expected.Event.OccurredAtUtc) { throw new ArgumentException("Score acknowledgement precedes publication intent."); }
        return context.ExecuteOwnerAsync(expected.Event.UserIdHash!, async deadline =>
        {
            var state = context.RequireState();
            var p = state.Publications.SingleOrDefault(p => p.CalculationId == expected.Event.EventId) ?? throw new StorageUnavailableException();
            var c = state.Calculations.Single(c => c.Id == p.CalculationId);
            if (ScorePersistenceValidation.Fingerprint(Map(state.Owner, c, p)) != ScorePersistenceValidation.Fingerprint(expected.Event))
            { throw new StorageConflictException(); }
            if (expected.SourceSequence is { } sequence && sequence != await context.WithRecordsAsync(state.Owner,
                (records, _) => OwnedPublicationOrder.ReadRequiredAsync(records, "score", state.Owner, p.CalculationId), deadline))
            { throw new StorageConflictException(); }
            if (p.PublishedAtUtc is not null) { return false; }
            state.Publications[state.Publications.IndexOf(p)] = p with { PublishedAtUtc = publishedAtUtc };
            return true;
        }, token);
    }

    internal static IntegrationEventEnvelope<ScoreCalculatedV1> Map(string owner, ScoreCalculationState calculation, ScorePublicationState publication) =>
        new(calculation.Id, publication.OccurrenceId, FinancialScoreEventTypes.ScoreCalculated, calculation.AtUtc,
            "financial-score-service", FinancialScoreEventTypes.SchemaVersion, publication.CorrelationId, calculation.Source, owner,
            new ScoreCalculatedV1(calculation.Id, calculation.Currency, calculation.Score, calculation.Formula,
                calculation.Factors.Select(f => new FinancialScoreFactorV1(f.Code, f.Contribution)).ToArray(), calculation.AtUtc));

    internal static string StableId(string prefix, string value) =>
        $"{prefix}-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()}";

    private sealed record ScanCursor([property: JsonRequired] string? After);
}
