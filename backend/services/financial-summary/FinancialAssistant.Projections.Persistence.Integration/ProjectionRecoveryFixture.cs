using System.Security.Cryptography;
using System.Text.Json;
using FinancialAssistant.Analytics.Infrastructure.Persistence;
using FinancialAssistant.FinancialSummary.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using Npgsql;

internal static class ProjectionRecoveryFixture
{
    private const string ProofBucket = "fixture_recovery_proof";
    public static string Stage { get; set; } = "not-started";

    public static async Task CaptureAsync(NativePostgresSettings summary, NativePostgresSettings analytics, NpgsqlConnection admin)
    {
        await VerifyGenerations(summary, analytics);
        foreach (var (owner, settings) in Owners(summary, analytics))
        {
            using var protection = settings.CreateProtection();
            await using var database = new OwnedPostgresStore(settings.CreateOptions(owner), protection);
            Check(await database.IsReadyAsync());
            var snapshot = await Snapshot(admin, owner);
            await database.ExecuteAsync([], async (records, _) =>
            {
                await records.InsertAsync(ProofBucket, owner, "snapshot", OwnedRecordCodec.Encode(1,
                    new ProjectionRecoveryProof(owner, snapshot.Rows, snapshot.Digest)));
                return true;
            });
        }
    }

    public static async Task VerifyAsync(NativePostgresSettings summary, NativePostgresSettings analytics,
        NpgsqlConnection admin, Func<Task> replay)
    {
        await VerifySnapshots(summary, analytics, admin, "before");
        await replay();
        Stage = "generation-read";
        await VerifyGenerations(summary, analytics);
        await VerifySnapshots(summary, analytics, admin, "after");
    }

    private static async Task VerifySnapshots(NativePostgresSettings summary, NativePostgresSettings analytics, NpgsqlConnection admin, string phase)
    {
        foreach (var (owner, settings) in Owners(summary, analytics))
        {
            Stage = phase + "-" + owner;
            using var protection = settings.CreateProtection();
            await using var database = new OwnedPostgresStore(settings.CreateOptions(owner), protection);
            Check(await database.IsReadyAsync());
            var proof = await database.ExecuteAsync([], async (records, _) =>
                OwnedRecordCodec.Decode<ProjectionRecoveryProof>(1, await records.GetAsync(ProofBucket, owner, "snapshot") ?? throw new FixtureFailure()));
            var snapshot = await Snapshot(admin, owner);
            Check(proof.Owner == owner && proof.Rows == snapshot.Rows && proof.Digest == snapshot.Digest);
        }
    }

    private static async Task<ProjectionRecoveryProof> Snapshot(NpgsqlConnection admin, string owner)
    {
        var identity = new StorageIdentity(owner);
        // Exclude the encrypted self-proof; every other record, including maintenance cursors, must remain identical.
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
            var row = new ProjectionSnapshotItem(reader.GetString(0), Convert.ToHexString(ownerHash), Convert.ToHexString(recordHash),
                reader.GetInt64(3), Convert.ToHexString(SHA256.HashData(payload)));
            var encoded = JsonSerializer.SerializeToUtf8Bytes(row);
            try { hash.AppendData(encoded); }
            finally { CryptographicOperations.ZeroMemory(encoded); }
        }
        Check(count > 1000);
        return new ProjectionRecoveryProof(owner, count, Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static async Task VerifyGenerations(NativePostgresSettings summarySettings, NativePostgresSettings analyticsSettings)
    {
        using var summaryProtection = summarySettings.CreateProtection();
        await using var summaryDatabase = new OwnedPostgresStore(summarySettings.CreateOptions("financial-summary"), summaryProtection);
        var summary = new PostgresFinancialSummaryReadModelStore(summaryDatabase);
        var owner = new string('f', 64);
        Check((await summary.GetRebuildAsync(owner)) is { Status: "running", StagedRecords: 100 });
        var usd = await summary.ListAsync(owner, "USD", default);
        Check(usd.Count == 4 && usd.Single(value => value.RecordId == "synthetic-rebuild-000").Amount == 10m
            && usd.Single(value => value.RecordId == "synthetic-live-during-rebuild").Amount == 3.22m);
        Check((await summary.ListAsync(owner, "EUR", default)).Single().Amount == 8.011m);
        Check((await summary.ListAsync(new string('1', 64), "USD", default)).Single().Amount == 33m);

        using var analyticsProtection = analyticsSettings.CreateProtection();
        await using var analyticsDatabase = new OwnedPostgresStore(analyticsSettings.CreateOptions("analytics"), analyticsProtection);
        var analytics = new PostgresAnalyticsReadModelStore(new AnalyticsPersistenceContext(analyticsDatabase));
        var analyticsOwner = new string('7', 64);
        Check((await analytics.GetRebuildAsync(analyticsOwner)) is { Status: "running", StagedRecords: 100 });
        var day = new DateOnly(2026, 10, 20); var month = new DateOnly(2026, 10, 1);
        var current = await analytics.GetAsync(analyticsOwner, "USD", default);
        Check(current.DailyTotals[day].Expense == 16.5m && current.MonthlyTotals[month].Totals.Income == 12.111m);
        Check((await analytics.GetAsync(analyticsOwner, "EUR", default)).MonthlyTotals[month].Totals.Expense == 4.11m);
        Check((await analytics.GetAsync(new string('2', 64), "USD", default)).DailyTotals[day].Income == 8m);
    }

    private static (string Owner, NativePostgresSettings Settings)[] Owners(NativePostgresSettings summary, NativePostgresSettings analytics) =>
        [("financial-summary", summary), ("analytics", analytics)];
    private static void Check(bool value) { if (!value) { throw new FixtureFailure(); } }
    private sealed record ProjectionRecoveryProof(string Owner, int Rows, string Digest);
    private sealed record ProjectionSnapshotItem(string Bucket, string Owner, string Record, long Revision, string Digest);
}
