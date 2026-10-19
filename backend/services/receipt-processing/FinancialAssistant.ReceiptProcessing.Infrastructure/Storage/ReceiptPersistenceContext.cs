using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

public sealed class ReceiptPersistenceContext
{
    private readonly OwnedPostgresStore database;
    private readonly AsyncLocal<Scope?> current = new();

    public ReceiptPersistenceContext(OwnedPostgresStore database)
    {
        if (database.ServiceId != "receipt-processing") { throw new ArgumentException("Receipt requires its owned storage role."); }
        this.database = database;
    }

    public Task<T> RunAsync<T>(string owner, Func<CancellationToken, Task<T>> operation, CancellationToken token = default)
    {
        ReceiptRecordCodec.Identifier(owner);
        if (current.Value is { } scope)
        {
            if (scope.Owner != owner) { throw new StorageUnavailableException(); }
            return operation(token);
        }
        return database.ExecuteAsync(new[] { "owner:" + owner }, async (records, deadline) =>
        {
            current.Value = new Scope(owner, records);
            try { return await operation(deadline); }
            finally { current.Value = null; }
        }, token);
    }

    public Task<T> WithRecordsAsync<T>(string owner, Func<OwnedRecordTransaction, CancellationToken, Task<T>> operation,
        CancellationToken token = default) => RunAsync(owner,
        deadline => operation(current.Value?.Records ?? throw new StorageUnavailableException(), deadline), token);

    private sealed record Scope(string Owner, OwnedRecordTransaction Records);
}
