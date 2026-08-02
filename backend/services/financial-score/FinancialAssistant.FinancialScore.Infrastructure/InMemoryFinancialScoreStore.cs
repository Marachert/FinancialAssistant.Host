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

    public Task<FinancialScoreProjectionWriteResult> UpsertProjectionAsync(
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
                    return Task.FromResult(FinancialScoreProjectionWriteResult.Duplicate);
                }

                if (current.Revision >= projection.Revision)
                {
                    return Task.FromResult(FinancialScoreProjectionWriteResult.Stale);
                }
            }

            projections[key] = projection;
            return Task.FromResult(FinancialScoreProjectionWriteResult.Applied);
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
            calculationsBySourceEvent.TryAdd(calculation.SourceEventId, calculation);
        }

        return Task.CompletedTask;
    }

    public Task<FinancialScoreCalculation?> GetBySourceEventIdAsync(
        string sourceEventId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(calculationsBySourceEvent.GetValueOrDefault(sourceEventId));
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
                Query(userIdHash, currency)
                    .OrderByDescending(item => item.CalculatedAtUtc)
                    .ThenByDescending(item => item.CalculationId, StringComparer.Ordinal)
                    .FirstOrDefault());
        }
    }

    public Task<IReadOnlyList<FinancialScoreCalculation>> GetHistoryAsync(
        string userIdHash,
        string currency,
        DateTimeOffset? beforeUtc,
        int limit,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            IReadOnlyList<FinancialScoreCalculation> result = Query(userIdHash, currency)
                .Where(item => beforeUtc is null || item.CalculatedAtUtc < beforeUtc.Value)
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
}
