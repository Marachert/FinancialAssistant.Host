using System.Security.Cryptography;
using System.Text.Json;
using FinancialAssistant.Shared.Persistence;
using Npgsql;

internal static class ReceiptAiRecoveryFixture
{
    private const string ProofBucket = "fixture_recovery_proof";

    internal static async Task CaptureAsync(NativePostgresSettings receipt, NativePostgresSettings ai, NpgsqlConnection admin)
    {
        foreach (var (owner, settings) in Owners(receipt, ai))
        {
            using var protection = settings.CreateProtection();
            await using var database = new OwnedPostgresStore(settings.CreateOptions(owner), protection);
            Check(await database.IsReadyAsync());
            var snapshot = await Snapshot(admin, owner, 0);
            await database.ExecuteAsync([], async (records, _) =>
            {
                await records.InsertAsync(ProofBucket, owner, "snapshot", OwnedRecordCodec.Encode(1, snapshot));
                return true;
            });
        }
    }

    internal static async Task VerifyAsync(NativePostgresSettings receipt, NativePostgresSettings ai,
        NpgsqlConnection admin, Func<Task> replay)
    {
        await VerifySnapshots();
        await replay();
        await VerifySnapshots();

        async Task VerifySnapshots()
        {
            foreach (var (owner, settings) in Owners(receipt, ai))
            {
                using var protection = settings.CreateProtection();
                await using var database = new OwnedPostgresStore(settings.CreateOptions(owner), protection);
                Check(await database.IsReadyAsync());
                var proof = await database.ExecuteAsync([], async (records, _) =>
                    OwnedRecordCodec.Decode<RecoveryProof>(1, await records.GetAsync(ProofBucket, owner, "snapshot") ?? throw new FixtureFailure()));
                var snapshot = await Snapshot(admin, owner, 1);
                Check(proof == snapshot);
            }
        }
    }

    private static async Task<RecoveryProof> Snapshot(NpgsqlConnection admin, string owner, int expectedProofs)
    {
        var identity = new StorageIdentity(owner);
        await using (var proofCount = new NpgsqlCommand($"SELECT count(*) FROM {identity.Schema}.records WHERE bucket=$1", admin))
        {
            proofCount.Parameters.AddWithValue(ProofBucket);
            Check((long)(await proofCount.ExecuteScalarAsync())! == expectedProofs);
        }
        // Include request claims, quota windows, publication acknowledgements and maintenance cursors.
        await using var command = new NpgsqlCommand($"SELECT bucket,owner_hash,record_hash,revision,octet_length(payload),payload FROM {identity.Schema}.records WHERE bucket<>$1 ORDER BY bucket,owner_hash,record_hash LIMIT 20001", admin);
        command.Parameters.AddWithValue(ProofBucket);
        await using var reader = await command.ExecuteReaderAsync();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(JsonSerializer.SerializeToUtf8Bytes(new { owner, format = 1 }));
        var count = 0; long bytes = 0;
        while (await reader.ReadAsync())
        {
            Check(++count <= 20000);
            var length = reader.GetInt32(4); Check(length is > 0 and <= 1048576);
            bytes += length; Check(bytes <= 64L * 1024 * 1024);
            var ownerHash = reader.GetFieldValue<byte[]>(1); var recordHash = reader.GetFieldValue<byte[]>(2);
            Check(ownerHash.Length == 32 && recordHash.Length == 32 && reader.GetInt64(3) >= 0);
            var payload = reader.GetFieldValue<byte[]>(5); Check(payload.Length == length);
            var encoded = JsonSerializer.SerializeToUtf8Bytes(new SnapshotItem(reader.GetString(0),
                Convert.ToHexString(ownerHash), Convert.ToHexString(recordHash), reader.GetInt64(3), Convert.ToHexString(SHA256.HashData(payload))));
            try { hash.AppendData(encoded); }
            finally { CryptographicOperations.ZeroMemory(encoded); }
        }
        Check(count > 4);
        return new RecoveryProof(owner, count, Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static (string Owner, NativePostgresSettings Settings)[] Owners(NativePostgresSettings receipt, NativePostgresSettings ai) =>
        [("receipt-processing", receipt), ("ai-orchestration", ai)];
    private static void Check(bool value) { if (!value) { throw new FixtureFailure(); } }
    private sealed record RecoveryProof(string Owner, int Rows, string Digest);
    private sealed record SnapshotItem(string Bucket, string Owner, string Record, long Revision, string Digest);
}
