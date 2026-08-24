namespace FinancialAssistant.Audit.Application;

public sealed class InMemoryAppendOnlyAuditRecordStore : IAuditRecordStore
{
    private readonly object sync = new();
    private readonly List<AuditRecord> records = [];
    private readonly Dictionary<string, AuditRecord> bySourceEvent = new(StringComparer.Ordinal);

    public Task<AuditAppendResult> AppendAsync(
        AuditRecord record,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (bySourceEvent.TryGetValue(record.SourceEventId, out var current))
            {
                if (!SameSourceEvent(current, record))
                {
                    throw new InvalidOperationException(
                        "A source event cannot replace an existing audit record.");
                }

                return Task.FromResult(AuditAppendResult.AlreadyPresent);
            }

            records.Add(record);
            bySourceEvent.Add(record.SourceEventId, record);
            return Task.FromResult(AuditAppendResult.Appended);
        }
    }

    public Task<IReadOnlyList<AuditRecord>> FindByCorrelationAsync(
        string correlationId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            IReadOnlyList<AuditRecord> result = records
                .Where(record => record.ExpiresAtUtc > asOfUtc)
                .Where(record => string.Equals(
                    record.CorrelationId,
                    correlationId,
                    StringComparison.Ordinal))
                .OrderBy(record => record.OccurredAtUtc)
                .ThenBy(record => record.AuditId, StringComparer.Ordinal)
                .ToArray();
            return Task.FromResult(result);
        }
    }

    private static bool SameSourceEvent(AuditRecord current, AuditRecord candidate) =>
        current with { RecordedAtUtc = candidate.RecordedAtUtc } == candidate;
}
