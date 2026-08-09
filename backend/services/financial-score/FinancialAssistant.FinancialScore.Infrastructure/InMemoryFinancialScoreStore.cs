using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Domain;

namespace FinancialAssistant.FinancialScore.Infrastructure;

public sealed class InMemoryFinancialScoreStore : IFinancialScoreStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, FinancialScoreRecordProjection> projections =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, FinancialScoreCalculation> calculationsBySourceEvent =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, FinancialScoreCalculation> currentByScope =
        new(StringComparer.Ordinal);

    public Task<FinancialScoreProjectionWriteOutcome> UpsertProjectionAsync(
        FinancialScoreRecordProjection projection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projection);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var key = ProjectionKey(projection);
            if (projections.TryGetValue(key, out var current))
            {
                if (current.EventId == projection.EventId)
                {
                    return Task.FromResult(
                        new FinancialScoreProjectionWriteOutcome(
                            FinancialScoreProjectionWriteResult.Duplicate,
                            current.Currency));
                }

                if (current.Revision >= projection.Revision)
                {
                    return Task.FromResult(
                        new FinancialScoreProjectionWriteOutcome(
                            FinancialScoreProjectionWriteResult.Stale,
                            current.Currency));
                }
            }

            var previousCurrency = current?.Currency;
            projections[key] = projection;
            return Task.FromResult(
                new FinancialScoreProjectionWriteOutcome(
                    FinancialScoreProjectionWriteResult.Applied,
                    previousCurrency));
        }
    }

    public Task<FinancialScoreSnapshot> GetSnapshotAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var normalizedCurrency = currency.ToUpperInvariant();
            return Task.FromResult(
                new FinancialScoreSnapshot(
                    userIdHash,
                    normalizedCurrency,
                    projections.Values
                        .Where(item => item.UserIdHash == userIdHash &&
                            item.Currency == normalizedCurrency)
                        .OrderBy(item => item.RecordType, StringComparer.Ordinal)
                        .ThenBy(item => item.RecordId, StringComparer.Ordinal)
                        .ToArray()));
        }
    }

    public Task SaveCalculationAsync(
        FinancialScoreCalculation calculation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calculation);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var key = CalculationKey(calculation.SourceEventId, calculation.Currency);
            if (calculationsBySourceEvent.TryAdd(key, calculation))
            {
                currentByScope[ScopeKey(calculation.UserIdHash, calculation.Currency)] = calculation;
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FinancialScoreCalculation>> GetBySourceEventIdAsync(
        string sourceEventId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            IReadOnlyList<FinancialScoreCalculation> result = calculationsBySourceEvent.Values
                .Where(item => item.SourceEventId == sourceEventId)
                .OrderBy(item => item.Currency, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    public Task<FinancialScoreCalculation?> GetCurrentAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(
                currentByScope.GetValueOrDefault(ScopeKey(userIdHash, currency)));
        }
    }

    public Task<IReadOnlyList<FinancialScoreCalculation>> GetHistoryAsync(
        string userIdHash,
        string currency,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        DateTimeOffset? beforeUtc,
        string? beforeCalculationId,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            IReadOnlyList<FinancialScoreCalculation> result = Query(userIdHash, currency)
                .Where(item => IsWithinPeriod(item, fromUtc, toUtc))
                .Where(item => IsBeforeCursor(item, beforeUtc, beforeCalculationId))
                .OrderByDescending(item => item.CalculatedAtUtc)
                .ThenByDescending(item => item.CalculationId, StringComparer.Ordinal)
                .Take(limit)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    private IEnumerable<FinancialScoreCalculation> Query(string userIdHash, string currency) =>
        calculationsBySourceEvent.Values.Where(item =>
            item.UserIdHash == userIdHash &&
            item.Currency == currency.ToUpperInvariant());

    private static string ProjectionKey(FinancialScoreRecordProjection projection) =>
        $"{projection.UserIdHash}|{projection.RecordType}|{projection.RecordId}";

    private static string CalculationKey(string sourceEventId, string currency) =>
        $"{sourceEventId}|{currency.ToUpperInvariant()}";

    private static string ScopeKey(string userIdHash, string currency) =>
        $"{userIdHash}|{currency.ToUpperInvariant()}";

    private static bool IsWithinPeriod(
        FinancialScoreCalculation calculation,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc) =>
        (fromUtc is null || calculation.CalculatedAtUtc >= fromUtc.Value) &&
        (toUtc is null || calculation.CalculatedAtUtc <= toUtc.Value);

    private static bool IsBeforeCursor(
        FinancialScoreCalculation calculation,
        DateTimeOffset? beforeUtc,
        string? beforeCalculationId)
    {
        if (beforeUtc is null)
        {
            return true;
        }

        return calculation.CalculatedAtUtc < beforeUtc.Value ||
            (calculation.CalculatedAtUtc == beforeUtc.Value &&
             string.CompareOrdinal(calculation.CalculationId, beforeCalculationId) < 0);
    }
}
