using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.FinancialScore.Infrastructure.Persistence;

public sealed class PostgresFinancialScoreStore(ScorePersistenceContext context) : IFinancialScoreStore
{
    public Task<FinancialScoreProjectionWriteOutcome> UpsertProjectionAsync(FinancialScoreRecordProjection projection, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ScorePersistenceValidation.Projection(projection);
        if (ScorePersistenceContext.ReadSource(context.RequireSource()) != projection) { throw new StorageConflictException(); }
        var state = context.RequireState(projection.UserIdHash);
        var incoming = ScoreProjectionState.From(projection);
        var previous = state.Projections.SingleOrDefault(p => p.Type == incoming.Type && p.Id == incoming.Id);
        if (previous is not null && previous.Revision >= incoming.Revision)
        {
            if (previous.Revision == incoming.Revision && previous != incoming) { throw new StorageConflictException(); }
            context.ExpectCurrencies([]);
            return Task.FromResult(new FinancialScoreProjectionWriteOutcome(
                previous == incoming ? FinancialScoreProjectionWriteResult.Duplicate : FinancialScoreProjectionWriteResult.Stale, previous.Currency));
        }
        if (previous is not null && (incoming.ChangedAtUtc < previous.ChangedAtUtc || incoming.EventId == previous.EventId))
        { throw new StorageConflictException(); }
        if (previous is not null) { state.Projections.Remove(previous); }
        state.Projections.Add(incoming);
        context.ExpectCurrencies(new[] { previous?.Currency, incoming.Currency }.Where(c => c is not null).Cast<string>().Distinct(StringComparer.Ordinal).ToArray());
        return Task.FromResult(new FinancialScoreProjectionWriteOutcome(FinancialScoreProjectionWriteResult.Applied, previous?.Currency));
    }

    public Task<FinancialScoreSnapshot> GetSnapshotAsync(string userIdHash, string currency, CancellationToken cancellationToken)
    {
        ScorePersistenceValidation.Currency(currency);
        return context.ReadAsync(userIdHash, state => new FinancialScoreSnapshot(userIdHash, currency,
            state.Projections.Where(p => p.Currency == currency).Select(p => p.ToDomain(userIdHash)).ToArray()), cancellationToken);
    }

    public Task SaveCalculationAsync(FinancialScoreCalculation calculation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ScorePersistenceValidation.Calculation(calculation);
        var state = context.RequireState(calculation.UserIdHash);
        var value = ScoreCalculationState.From(calculation);
        if (context.CurrentSource is { } source)
        {
            if (value.Source != source.EventId || value.AtUtc != source.Payload.ChangedAtUtc ||
                value.Id != PostgresFinancialScoreEventPublisher.StableId("score", $"{source.EventId}|{value.Currency}"))
            { throw new StorageConflictException(); }
        }
        else if (value.Source != PostgresFinancialScoreEventPublisher.StableId("score-default-source", $"{state.Owner}|{value.Currency}") ||
            value.Id != PostgresFinancialScoreEventPublisher.StableId("score-default", $"{state.Owner}|{value.Currency}|{value.Formula}") ||
            value.Score != FinancialScoreFormula.NewUserDefault || state.Projections.Any(p => p.Currency == value.Currency))
        { throw new StorageConflictException(); }
        var existing = state.Calculations.SingleOrDefault(c => c.Id == value.Id);
        if (existing is not null)
        {
            if (ScorePersistenceValidation.Fingerprint(existing) != ScorePersistenceValidation.Fingerprint(value)) { throw new StorageConflictException(); }
            return Task.CompletedTask;
        }
        if (state.Calculations.Any(c => c.Source == value.Source && c.Currency == value.Currency)) { throw new StorageConflictException(); }
        state.Calculations.Add(value);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FinancialScoreCalculation>> GetBySourceEventIdAsync(string sourceEventId, CancellationToken cancellationToken)
    {
        var source = context.RequireSource();
        if (source.EventId != sourceEventId) { throw new StorageUnavailableException(); }
        return context.ReadAsync<IReadOnlyList<FinancialScoreCalculation>>(source.UserIdHash!,
            state => state.Calculations.Where(c => c.Source == sourceEventId).Select(c => c.ToDomain(state.Owner)).ToArray(), cancellationToken);
    }

    public Task<FinancialScoreCalculation?> GetCurrentAsync(string userIdHash, string currency, CancellationToken cancellationToken)
    {
        ScorePersistenceValidation.Currency(currency);
        // The last committed calculation is current even when the triggering record timestamp is older.
        return context.ReadAsync(userIdHash, state => state.Calculations.LastOrDefault(c => c.Currency == currency)?.ToDomain(userIdHash), cancellationToken);
    }

    public Task<IReadOnlyList<FinancialScoreCalculation>> GetHistoryAsync(string userIdHash, string currency,
        DateTimeOffset? fromUtc, DateTimeOffset? toUtc, DateTimeOffset? beforeUtc, string? beforeCalculationId, int limit,
        CancellationToken cancellationToken)
    {
        ScorePersistenceValidation.Currency(currency);
        if (limit is < 1 or > 101 || (fromUtc is null) != (toUtc is null) || fromUtc > toUtc ||
            (beforeUtc is null) != (beforeCalculationId is null)) { throw new ArgumentException("Invalid Score history bounds."); }
        if (beforeCalculationId is not null) { ScorePersistenceValidation.Identifier(beforeCalculationId); }
        return context.ReadAsync<IReadOnlyList<FinancialScoreCalculation>>(userIdHash, state => state.Calculations
            .Where(c => c.Currency == currency && (fromUtc is null || c.AtUtc >= fromUtc && c.AtUtc <= toUtc))
            .Where(c => beforeUtc is null || c.AtUtc < beforeUtc || c.AtUtc == beforeUtc && string.CompareOrdinal(c.Id, beforeCalculationId) < 0)
            .OrderByDescending(c => c.AtUtc).ThenByDescending(c => c.Id, StringComparer.Ordinal).Take(limit)
            .Select(c => c.ToDomain(userIdHash)).ToArray(), cancellationToken);
    }
}
