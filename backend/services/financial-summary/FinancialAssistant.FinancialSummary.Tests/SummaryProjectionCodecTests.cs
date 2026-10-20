using System.Text.Json.Nodes;
using FinancialAssistant.FinancialSummary.Domain;
using FinancialAssistant.FinancialSummary.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using Xunit;

namespace FinancialAssistant.FinancialSummary.Tests;

public sealed class SummaryProjectionCodecTests
{
    private static FinancialRecordProjection Projection() => new("expense", "synthetic-record", new string('a', 64),
        12.3401m, "USD", "expense.groceries", new DateOnly(2026, 10, 20), "active", 0,
        "confirmed_transaction", new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero), "synthetic-event");

    [Fact]
    public void ExactProjectionRoundTripsWithoutConflatingStorageAndSourceRevisions()
    {
        var value = Projection() with { Revision = 31, Status = "archived" };
        var stored = new DurableRecord(7, SummaryProjectionCodec.Encode(value));
        Assert.Equal(value, SummaryProjectionCodec.Decode(stored, value.UserIdHash, "expense:synthetic-record"));
        Assert.Equal(12.3401m, SummaryProjectionCodec.Decode(stored, value.UserIdHash).Amount);
    }

    [Theory]
    [InlineData("recordType")]
    [InlineData("recordId")]
    [InlineData("userIdHash")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("categoryId")]
    [InlineData("date")]
    [InlineData("status")]
    [InlineData("revision")]
    [InlineData("origin")]
    [InlineData("changedAtUtc")]
    [InlineData("eventId")]
    public void MissingPersistentFieldsAreNotDefaulted(string field)
    {
        var json = JsonNode.Parse(SummaryProjectionCodec.Encode(Projection()))!;
        Assert.True(json["value"]!.AsObject().Remove(field));
        Assert.Throws<StorageUnavailableException>(() => SummaryProjectionCodec.Decode(
            new DurableRecord(0, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(json)), Projection().UserIdHash));
    }

    [Fact]
    public void UnknownPersistentFieldsAndWrongVersionFailClosed()
    {
        var value = Projection();
        var json = JsonNode.Parse(SummaryProjectionCodec.Encode(value))!;
        json["value"]!["rawProviderText"] = "synthetic unwanted field";
        Assert.Throws<StorageUnavailableException>(() => SummaryProjectionCodec.Decode(
            new DurableRecord(0, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(json)), value.UserIdHash));
        json["value"]!.AsObject().Remove("rawProviderText");
        json["version"] = 2;
        Assert.Throws<StorageUnavailableException>(() => SummaryProjectionCodec.Decode(
            new DurableRecord(0, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(json)), value.UserIdHash));
    }

    [Fact]
    public void ForeignOwnerAndRecordKeyAreRejected()
    {
        var value = Projection();
        var stored = new DurableRecord(0, SummaryProjectionCodec.Encode(value));
        Assert.Throws<StorageUnavailableException>(() => SummaryProjectionCodec.Decode(stored, new string('b', 64)));
        Assert.Throws<StorageUnavailableException>(() => SummaryProjectionCodec.Decode(stored, value.UserIdHash, "income:synthetic-record"));
    }

    [Fact]
    public void DuplicateIsNoOpAndChangedSameRevisionIsConflict()
    {
        var value = Projection();
        Assert.False(SummaryProjectionCodec.ShouldReplace(value, value));
        Assert.Throws<StorageConflictException>(() => SummaryProjectionCodec.ShouldReplace(value, value with { Amount = 10m }));
        Assert.Throws<StorageConflictException>(() => SummaryProjectionCodec.ShouldReplace(value, value with { EventId = "other-event" }));
    }

    [Fact]
    public void StaleEventsCannotUndoCurrencyMoveArchiveOrNewerRevision()
    {
        var initial = Projection();
        var next = initial with
        {
            Currency = "EUR",
            Status = "archived",
            Revision = 3,
            ChangedAtUtc = initial.ChangedAtUtc.AddHours(1),
            EventId = "new-event"
        };
        Assert.True(SummaryProjectionCodec.ShouldReplace(initial, next));
        Assert.False(SummaryProjectionCodec.ShouldReplace(next, initial));
        Assert.True(SummaryProjectionCodec.ShouldReplace(null, next));
    }

    [Fact]
    public void IdentityOriginEventIdAndClockCannotBeRewrittenOnNewRevision()
    {
        var current = Projection();
        foreach (var next in new[]
        {
            current with { UserIdHash = new string('b', 64), Revision = 1, EventId = "next" },
            current with { RecordId = "other", Revision = 1, EventId = "next" },
            current with { Origin = "manual", Revision = 1, EventId = "next" },
            current with { Revision = 1, ChangedAtUtc = current.ChangedAtUtc.AddTicks(-1), EventId = "next" },
            current with { Revision = 1 }
        })
        { Assert.Throws<StorageConflictException>(() => SummaryProjectionCodec.ShouldReplace(current, next)); }
    }

    [Fact]
    public void InvalidFinancialOrPrivacyShapesCannotEnterNativeProjection()
    {
        var value = Projection();
        foreach (var invalid in new[]
        {
            value with { Amount = 0 }, value with { Amount = -1 }, value with { Currency = "usd" },
            value with { Currency = "U1D" }, value with { CategoryId = "income.salary" },
            value with { RecordId = "unsafe/record" }, value with { EventId = "event\n" },
            value with { UserIdHash = new string('A', 64) }, value with { Origin = "llm" },
            value with { Status = "deleted" }, value with { RecordType = "draft" },
            value with { Revision = -1 }, value with { Revision = long.MaxValue },
            value with { Date = default }, value with { ChangedAtUtc = default },
            value with { ChangedAtUtc = value.ChangedAtUtc.ToOffset(TimeSpan.FromHours(1)) }
        })
        { Assert.Throws<ArgumentException>(() => SummaryProjectionCodec.Encode(invalid)); }
    }

    [Fact]
    public void SourceDigestIsCanonicalBoundedAndSensitiveToEveryProjectionValue()
    {
        var first = Projection();
        var second = first with { RecordId = "other-record", EventId = "other-event", Currency = "EUR" };
        var digest = PostgresFinancialSummaryReadModelStore.CalculateSourceDigest(new[] { first, second });
        Assert.Equal(digest, PostgresFinancialSummaryReadModelStore.CalculateSourceDigest(new[] { second, first }));
        Assert.NotEqual(digest, PostgresFinancialSummaryReadModelStore.CalculateSourceDigest(new[] { first with { Amount = 12.3402m }, second }));
        Assert.Equal(SummaryProjectionCodec.Fingerprint(first with { Amount = 1.00m }), SummaryProjectionCodec.Fingerprint(first with { Amount = 1m }));
        Assert.Throws<ArgumentException>(() => PostgresFinancialSummaryReadModelStore.CalculateSourceDigest(new[] { first, first }));
        Assert.Throws<ArgumentException>(() => PostgresFinancialSummaryReadModelStore.CalculateSourceDigest(
            Enumerable.Range(0, 1001).Select(i => first with { RecordId = "record-" + i })));
    }

    [Fact]
    public async Task ModuleFactoryKeepsItsOwnStorageWithoutAnAnalyticsDependencyOrHost()
    {
        var settings = new NativePostgresSettings
        {
            Database = "synthetic_summary",
            Port = 5432,
            Password = new string('x', 40),
            IndexKey = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()),
            ActiveEncryptionKeyId = "v1",
            EncryptionKeys = new Dictionary<string, string> { ["v1"] = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray()) }
        };
        await using var module = new NativeFinancialSummaryStorage(settings);
        Assert.IsType<PostgresFinancialSummaryReadModelStore>(module.Store);
        await Assert.ThrowsAsync<NotSupportedException>(() => module.Store.ResetAsync(default));
    }

    [Fact]
    public async Task InvalidRebuildManifestsFailBeforeAnyDatabaseAccess()
    {
        using var protection = new RecordProtection(Enumerable.Repeat((byte)1, 32).ToArray(), "v1",
            new Dictionary<string, byte[]> { ["v1"] = Enumerable.Repeat((byte)2, 32).ToArray() });
        await using var database = new OwnedPostgresStore(new OwnedPostgresOptions(new StorageIdentity("financial-summary"),
            "synthetic_summary", 5432, new string('x', 64)), protection);
        var store = new PostgresFinancialSummaryReadModelStore(database);
        var projection = Projection();
        var request = new SummaryRebuildRequest(projection.UserIdHash, projection.Date, projection.Date, "synthetic-v1",
            projection.ChangedAtUtc, 1, PostgresFinancialSummaryReadModelStore.CalculateSourceDigest(new[] { projection }));
        foreach (var invalid in new[]
        {
            request with { OwnerScopeHash = "invalid" }, request with { PeriodStart = default },
            request with { PeriodEnd = request.PeriodStart.AddDays(-1) },
            request with { PeriodEnd = request.PeriodStart.AddDays(3650) },
            request with { SourceSnapshotVersion = " " }, request with { SourceSnapshotVersion = "v1\n" },
            request with { SourceSnapshotVersion = new string('x', 129) },
            request with { RequestedAtUtc = default }, request with { RequestedAtUtc = request.RequestedAtUtc.ToOffset(TimeSpan.FromHours(1)) },
            request with { ExpectedRecords = -1 }, request with { ExpectedRecords = 1001 },
            request with { ExpectedDigest = new string('z', 64) }, request with { ExpectedRecords = 0 }
        })
        { await Assert.ThrowsAsync<ArgumentException>(() => store.BeginRebuildAsync(invalid)); }
    }

    [Fact]
    public async Task NativeStoreRejectsBorrowedBusinessRoleAndGlobalResetWithoutConnecting()
    {
        using var protection = new RecordProtection(Enumerable.Repeat((byte)1, 32).ToArray(), "v1",
            new Dictionary<string, byte[]> { ["v1"] = Enumerable.Repeat((byte)2, 32).ToArray() });
        await using var wrong = new OwnedPostgresStore(new OwnedPostgresOptions(new StorageIdentity("analytics"),
            "synthetic_summary", 5432, new string('x', 64)), protection);
        Assert.Throws<ArgumentException>(() => new PostgresFinancialSummaryReadModelStore(wrong));
        await using var database = new OwnedPostgresStore(new OwnedPostgresOptions(new StorageIdentity("financial-summary"),
            "synthetic_summary", 5432, new string('x', 64)), protection);
        var store = new PostgresFinancialSummaryReadModelStore(database);
        await Assert.ThrowsAsync<NotSupportedException>(() => store.ResetAsync(CancellationToken.None));
    }
}
