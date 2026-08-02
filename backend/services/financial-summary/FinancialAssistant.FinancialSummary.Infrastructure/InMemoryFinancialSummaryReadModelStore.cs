using System.Collections.Concurrent;
using FinancialAssistant.FinancialSummary.Application;
using FinancialAssistant.FinancialSummary.Domain;

namespace FinancialAssistant.FinancialSummary.Infrastructure;

public sealed class InMemoryFinancialSummaryReadModelStore : IFinancialSummaryReadModelStore
{
    private readonly ConcurrentDictionary<string, FinancialRecordProjection> projections =
        new(StringComparer.Ordinal);

    public Task UpsertIfNewerAsync(
        FinancialRecordProjection projection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projection);
        cancellationToken.ThrowIfCancellationRequested();
        var key = CreateKey(projection.UserIdHash, projection.RecordType, projection.RecordId);
        projections.AddOrUpdate(
            key,
            projection,
            (_, current) => projection.Revision > current.Revision ? projection : current);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FinancialRecordProjection>> ListAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<FinancialRecordProjection> result = projections.Values
            .Where(projection =>
                projection.UserIdHash == userIdHash &&
                string.Equals(projection.Currency, currency, StringComparison.OrdinalIgnoreCase))
            .OrderBy(projection => projection.RecordType, StringComparer.Ordinal)
            .ThenBy(projection => projection.RecordId, StringComparer.Ordinal)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        projections.Clear();
        return Task.CompletedTask;
    }

    private static string CreateKey(string userIdHash, string recordType, string recordId) =>
        $"{userIdHash}|{recordType}|{recordId}";
}
