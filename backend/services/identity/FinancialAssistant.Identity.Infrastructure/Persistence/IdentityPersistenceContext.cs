using System.Text.Json;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Identity.Infrastructure.Persistence;

public sealed class IdentityPersistenceContext
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly OwnedPostgresStore store;
    private readonly AsyncLocal<OwnedRecordTransaction?> transaction = new();

    public IdentityPersistenceContext(OwnedPostgresStore store) { this.store = store; }

    public Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (transaction.Value is not null) { return operation(cancellationToken); }
        return store.ExecuteAsync(new[] { "identity_mutations" }, async (records, token) =>
        {
            transaction.Value = records;
            try { return await operation(token); }
            finally { transaction.Value = null; }
        }, cancellationToken);
    }

    public Task<T> WithRecordsAsync<T>(Func<OwnedRecordTransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default) => RunAsync(token => operation(
            transaction.Value ?? throw new InvalidOperationException("Identity transaction is unavailable."), token), cancellationToken);

    internal static byte[] Encode<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(new Versioned<T>(1, value), Json);

    internal static T Decode<T>(DurableRecord record)
    {
        try
        {
            var value = JsonSerializer.Deserialize<Versioned<T>>(record.Payload, Json);
            if (value is null || value.Version != 1 || value.Value is null) { throw new StorageUnavailableException(); }
            return value.Value;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new StorageUnavailableException();
        }
    }

    internal static async Task<T?> ReadAsync<T>(OwnedRecordTransaction records, string bucket, string owner, string id)
    {
        var record = await records.GetAsync(bucket, owner, id);
        return record is null ? default : Decode<T>(record);
    }

    internal static async Task WriteAsync<T>(OwnedRecordTransaction records, string bucket, string owner, string id, T value)
    {
        var existing = await records.GetAsync(bucket, owner, id);
        if (existing is null) { await records.InsertAsync(bucket, owner, id, Encode(value)); }
        else { await records.ReplaceAsync(bucket, owner, id, existing.Revision, Encode(value)); }
    }

    private sealed record Versioned<T>(int Version, T Value);
}
