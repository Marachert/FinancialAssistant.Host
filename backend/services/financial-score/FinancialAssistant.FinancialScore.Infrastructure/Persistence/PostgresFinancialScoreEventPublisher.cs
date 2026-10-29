using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.FinancialScore.Infrastructure.Persistence;

public sealed record ScoreOutboxMessage(IntegrationEventEnvelope<ScoreCalculatedV1> Event, DateTimeOffset? PublishedAtUtc);

public sealed class PostgresFinancialScoreEventPublisher(ScorePersistenceContext context) : IFinancialScoreEventPublisher
{
    public Task PublishAsync(IntegrationEventEnvelope<ScoreCalculatedV1> envelope, CancellationToken cancellationToken)
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
        if (existing is null) { state.Publications.Add(candidate); }
        else if (existing.OccurrenceId != candidate.OccurrenceId || existing.CorrelationId != candidate.CorrelationId) { throw new StorageConflictException(); }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ScoreOutboxMessage>> ReadPendingAsync(string owner, CancellationToken token = default) =>
        context.ReadAsync<IReadOnlyList<ScoreOutboxMessage>>(owner, state => state.Publications.Where(p => p.PublishedAtUtc is null)
            .Take(25).Select(p => new ScoreOutboxMessage(Map(owner, state.Calculations.Single(c => c.Id == p.CalculationId), p), null)).ToArray(), token);

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
        return context.ExecuteOwnerAsync(expected.Event.UserIdHash!, _ =>
        {
            var state = context.RequireState();
            var p = state.Publications.SingleOrDefault(p => p.CalculationId == expected.Event.EventId) ?? throw new StorageUnavailableException();
            var c = state.Calculations.Single(c => c.Id == p.CalculationId);
            if (ScorePersistenceValidation.Fingerprint(Map(state.Owner, c, p)) != ScorePersistenceValidation.Fingerprint(expected.Event))
            { throw new StorageConflictException(); }
            if (p.PublishedAtUtc is not null) { return Task.FromResult(false); }
            state.Publications[state.Publications.IndexOf(p)] = p with { PublishedAtUtc = publishedAtUtc };
            return Task.FromResult(true);
        }, token);
    }

    internal static IntegrationEventEnvelope<ScoreCalculatedV1> Map(string owner, ScoreCalculationState calculation, ScorePublicationState publication) =>
        new(calculation.Id, publication.OccurrenceId, FinancialScoreEventTypes.ScoreCalculated, calculation.AtUtc,
            "financial-score-service", FinancialScoreEventTypes.SchemaVersion, publication.CorrelationId, calculation.Source, owner,
            new ScoreCalculatedV1(calculation.Id, calculation.Currency, calculation.Score, calculation.Formula,
                calculation.Factors.Select(f => new FinancialScoreFactorV1(f.Code, f.Contribution)).ToArray(), calculation.AtUtc));

    internal static string StableId(string prefix, string value) =>
        $"{prefix}-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()}";
}
