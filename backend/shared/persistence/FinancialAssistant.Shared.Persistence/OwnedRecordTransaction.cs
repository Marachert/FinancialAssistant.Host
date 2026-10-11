using System.Globalization;
using System.Text;
using Npgsql;

namespace FinancialAssistant.Shared.Persistence;

public sealed record DurableRecord(long Revision, byte[] Payload);

public sealed class OwnedRecordTransaction
{
    private readonly NpgsqlConnection connection;
    private readonly StorageIdentity identity;
    private readonly RecordProtection protection;
    private readonly CancellationToken token;
    private readonly object gate = new();
    private bool closed;
    private bool busy;
    private bool conflicted;

    internal OwnedRecordTransaction(NpgsqlConnection connection, StorageIdentity identity,
        RecordProtection protection, CancellationToken token)
    {
        this.connection = connection;
        this.identity = identity;
        this.protection = protection;
        this.token = token;
    }

    internal void Close() { lock (gate) { closed = true; } }

    internal void Seal()
    {
        lock (gate)
        {
            closed = true;
            if (busy) { throw new InvalidOperationException("Unfinished storage operation at commit."); }
            if (conflicted) { throw new StorageConflictException(); }
        }
    }

    private (byte[] Owner, byte[] Id) Hashes(string bucket, string owner, string id)
    {
        if (!RecordProtection.ValidName(bucket)) { throw new ArgumentException("Invalid storage bucket."); }
        return (protection.Index(identity.ServiceId, "owner", owner),
            protection.Index(identity.ServiceId, "record", bucket, owner, id));
    }

    private byte[] AssociatedData(string bucket, byte[] owner, byte[] id, long revision) => Encoding.ASCII.GetBytes(
        $"{identity.ServiceId}|{bucket}|{Convert.ToHexString(owner)}|{Convert.ToHexString(id)}|{revision.ToString(CultureInfo.InvariantCulture)}");

    private void Enter()
    {
        lock (gate)
        {
            if (closed) { throw new InvalidOperationException("Storage transaction has ended."); }
            if (conflicted) { throw new StorageConflictException(); }
            token.ThrowIfCancellationRequested();
            if (busy) { throw new InvalidOperationException("Concurrent operations on one storage transaction are forbidden."); }
            busy = true;
        }
    }

    private void Exit() { lock (gate) { busy = false; } }

    private bool TrackWrite(bool succeeded)
    {
        if (!succeeded) { lock (gate) { conflicted = true; } }
        return succeeded;
    }

    public async Task<DurableRecord?> GetAsync(string bucket, string owner, string id)
    {
        Enter();
        try
        {
            var hashes = Hashes(bucket, owner, id);
            await using var command = new NpgsqlCommand($"SELECT revision,payload FROM {identity.Schema}.records WHERE bucket=$1 AND owner_hash=$2 AND record_hash=$3", connection);
            command.Parameters.AddWithValue(bucket);
            command.Parameters.AddWithValue(hashes.Owner);
            command.Parameters.AddWithValue(hashes.Id);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) { return null; }
            var revision = reader.GetInt64(0);
            return new DurableRecord(revision, protection.Decrypt(reader.GetFieldValue<byte[]>(1),
                AssociatedData(bucket, hashes.Owner, hashes.Id, revision)));
        }
        finally { Exit(); }
    }

    public Task<bool> InsertAsync(string bucket, string owner, string id, byte[] payload) =>
        WriteAsync(bucket, owner, id, -1, payload);

    public Task<bool> ReplaceAsync(string bucket, string owner, string id, long expectedRevision, byte[] payload)
    {
        if (expectedRevision is < 0 or long.MaxValue) { throw new ArgumentOutOfRangeException(nameof(expectedRevision)); }
        return WriteAsync(bucket, owner, id, expectedRevision, payload);
    }

    private async Task<bool> WriteAsync(string bucket, string owner, string id, long expectedRevision, byte[] payload)
    {
        Enter();
        try
        {
            var hashes = Hashes(bucket, owner, id);
            var revision = expectedRevision + 1;
            var encrypted = protection.Encrypt(payload, AssociatedData(bucket, hashes.Owner, hashes.Id, revision));
            var sql = expectedRevision == -1
                ? $"INSERT INTO {identity.Schema}.records VALUES($1,$2,$3,$4,$5) ON CONFLICT DO NOTHING"
                : $"UPDATE {identity.Schema}.records SET revision=$4,payload=$5 WHERE bucket=$1 AND owner_hash=$2 AND record_hash=$3 AND revision=$6";
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue(bucket);
            command.Parameters.AddWithValue(hashes.Owner);
            command.Parameters.AddWithValue(hashes.Id);
            command.Parameters.AddWithValue(revision);
            command.Parameters.AddWithValue(encrypted);
            if (expectedRevision >= 0) { command.Parameters.AddWithValue(expectedRevision); }
            return TrackWrite(await command.ExecuteNonQueryAsync(token) == 1);
        }
        finally { Exit(); }
    }

    public async Task<bool> DeleteAsync(string bucket, string owner, string id, long expectedRevision)
    {
        if (expectedRevision < 0) { throw new ArgumentOutOfRangeException(nameof(expectedRevision)); }
        Enter();
        try
        {
            var hashes = Hashes(bucket, owner, id);
            await using var command = new NpgsqlCommand($"DELETE FROM {identity.Schema}.records WHERE bucket=$1 AND owner_hash=$2 AND record_hash=$3 AND revision=$4", connection);
            command.Parameters.AddWithValue(bucket);
            command.Parameters.AddWithValue(hashes.Owner);
            command.Parameters.AddWithValue(hashes.Id);
            command.Parameters.AddWithValue(expectedRevision);
            return TrackWrite(await command.ExecuteNonQueryAsync(token) == 1);
        }
        finally { Exit(); }
    }
}
