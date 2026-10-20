using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FinancialAssistant.Analytics.Application;
using FinancialAssistant.Analytics.Domain;
using FinancialAssistant.Analytics.Infrastructure;
using FinancialAssistant.Analytics.Infrastructure.Persistence;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;
using Xunit;

namespace FinancialAssistant.Analytics.Tests;

public sealed class NativeAnalyticsProjectionTests
{
    private static readonly string Owner = new('a', 64);
    private static readonly DateTimeOffset Time = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static AnalyticsRecordProjection Projection() => new("expense", "synthetic-record", Owner, 0.0001m,
        "USD", "expense.synthetic", new DateOnly(2026, 10, 20), "active", 0, Time, "synthetic-event");

    [Fact]
    public void ExactProjectionRoundTripsWithIndependentStorageRevisionAndUncategorizedFallback()
    {
        var value = Projection() with { Revision = 9, CategoryId = AnalyticsCategoryIds.Uncategorized };
        Assert.Equal(value, AnalyticsProjectionCodec.Decode(new DurableRecord(2, AnalyticsProjectionCodec.Encode(value)), Owner,
            "expense:synthetic-record"));
        var source = Source(Projection());
        var noCategory = new IntegrationEventEnvelope<FinancialRecordChangedV1>(source.EventId, source.OccurrenceId, source.EventType,
            Time, source.Producer, 1, source.CorrelationId, source.CausationId, Owner, source.Payload with { CategoryId = null! });
        Assert.Equal(AnalyticsCategoryIds.Uncategorized, AnalyticsPersistenceContext.ReadSource(noCategory).CategoryId);
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
    [InlineData("changedAtUtc")]
    [InlineData("eventId")]
    public void MissingSnapshotFieldsCannotBecomeDefaults(string field)
    {
        var json = JsonNode.Parse(AnalyticsProjectionCodec.Encode(Projection()))!;
        Assert.True(json["value"]!.AsObject().Remove(field));
        Assert.Throws<StorageUnavailableException>(() => AnalyticsProjectionCodec.Decode(
            new DurableRecord(0, JsonSerializer.SerializeToUtf8Bytes(json)), Owner));
    }

    [Fact]
    public void UnknownSnapshotMembersAndForeignIdentitiesFailClosed()
    {
        var json = JsonNode.Parse(AnalyticsProjectionCodec.Encode(Projection()))!;
        json["value"]!["rawInput"] = "synthetic unwanted input";
        Assert.Throws<StorageUnavailableException>(() => AnalyticsProjectionCodec.Decode(
            new DurableRecord(0, JsonSerializer.SerializeToUtf8Bytes(json)), Owner));
        var durable = new DurableRecord(0, AnalyticsProjectionCodec.Encode(Projection()));
        Assert.Throws<StorageUnavailableException>(() => AnalyticsProjectionCodec.Decode(durable, new string('b', 64)));
        Assert.Throws<StorageUnavailableException>(() => AnalyticsProjectionCodec.Decode(durable, Owner, "income:synthetic-record"));
    }

    [Fact]
    public void RevisionRulesRejectConflictsAndPreserveLaterCurrencyArchiveState()
    {
        var first = Projection();
        var next = first with { Revision = 1, EventId = "synthetic-next", Currency = "EUR", Status = "archived", ChangedAtUtc = Time.AddMinutes(1) };
        Assert.True(AnalyticsProjectionCodec.ShouldReplace(first, next));
        Assert.False(AnalyticsProjectionCodec.ShouldReplace(next, first));
        Assert.False(AnalyticsProjectionCodec.ShouldReplace(first, first));
        Assert.Equal(AnalyticsProjectionCodec.Fingerprint(first with { Amount = 1.00m }), AnalyticsProjectionCodec.Fingerprint(first with { Amount = 1m }));
        Assert.Throws<StorageConflictException>(() => AnalyticsProjectionCodec.ShouldReplace(first, first with { Amount = 2m }));
        Assert.Throws<StorageConflictException>(() => AnalyticsProjectionCodec.ShouldReplace(first, first with { EventId = "other" }));
        Assert.Throws<StorageConflictException>(() => AnalyticsProjectionCodec.ShouldReplace(first, next with { UserIdHash = new string('b', 64) }));
        Assert.Throws<StorageConflictException>(() => AnalyticsProjectionCodec.ShouldReplace(first, next with { EventId = first.EventId }));
        Assert.Throws<StorageConflictException>(() => AnalyticsProjectionCodec.ShouldReplace(first, next with { ChangedAtUtc = Time.AddTicks(-1) }));
    }

    [Fact]
    public void SnapshotBuilderPreservesExactPeriodCategoryAndArchivedFreshnessBehavior()
    {
        var first = Projection();
        var income = first with { RecordType = "income", RecordId = "synthetic-income", Amount = 10.1234m, CategoryId = "income.synthetic" };
        var prior = first with { RecordId = "synthetic-prior", Amount = 0.0002m, Date = first.Date.AddDays(-1) };
        var archived = first with { RecordId = "synthetic-archived", Amount = 999m, Status = "archived", ChangedAtUtc = Time.AddHours(1) };
        var foreign = first with { UserIdHash = new string('b', 64), Amount = 900m };
        var eur = first with { Currency = "EUR", Amount = 800m };
        var snapshot = AnalyticsSnapshotBuilder.Build(Owner, "USD", new[] { first, income, prior, archived, foreign, eur });
        Assert.Equal(10.1234m, snapshot.DailyTotals[first.Date].Income);
        Assert.Equal(0.0001m, snapshot.DailyTotals[first.Date].Expense);
        Assert.Equal(0.0003m, snapshot.WeeklyTotals[first.Date.AddDays(-1)].Expense);
        Assert.Equal(10.1231m, snapshot.MonthlyTotals[new DateOnly(2026, 10, 1)].Totals.BalanceDelta);
        Assert.Equal(2, snapshot.MonthlyTotals[new DateOnly(2026, 10, 1)].CategoryTotals.Count);
        Assert.Equal(archived.ChangedAtUtc, snapshot.LastEventAtUtc);
    }

    [Fact]
    public async Task StagedPublicationIsNotCompletionAndUsesApplicationTransactionBoundary()
    {
        var store = new InMemoryAnalyticsReadModelStore();
        var boundary = new RecordingTransaction();
        var publisher = new StagingPublisher();
        var projector = new AnalyticsProjector(store, publisher, null, boundary);
        await projector.ApplyAsync(Source(Projection()), default);
        Assert.Equal(1, boundary.Executions);
        Assert.True(boundary.Completed);
        Assert.Equal(1, publisher.Staged);
        Assert.Equal(new[] { "USD" }, (await store.UpsertIfNewerAsync(Projection(), default)).PendingPublicationCurrencies);
    }

    [Fact]
    public void InvalidFinancialShapesCannotEnterNativeProjection()
    {
        var first = Projection();
        foreach (var invalid in new[]
        {
            first with { Amount = 0 }, first with { Amount = -1 }, first with { Currency = "usd" },
            first with { Currency = "U1D" }, first with { CategoryId = "income.salary" },
            first with { RecordId = "unsafe/path" }, first with { UserIdHash = new string('A', 64) },
            first with { Revision = -1 }, first with { Revision = long.MaxValue }, first with { Date = default },
            first with { ChangedAtUtc = default }, first with { ChangedAtUtc = Time.ToOffset(TimeSpan.FromHours(1)) }
        })
        { Assert.Throws<ArgumentException>(() => AnalyticsProjectionCodec.Encode(invalid)); }
    }

    [Fact]
    public async Task NativeRoleResetCompletionAndPublisherContextFailClosedBeforeConnection()
    {
        using var protection = new RecordProtection(Enumerable.Repeat((byte)1, 32).ToArray(), "v1",
            new Dictionary<string, byte[]> { ["v1"] = Enumerable.Repeat((byte)2, 32).ToArray() });
        await using var wrong = new OwnedPostgresStore(new OwnedPostgresOptions(new StorageIdentity("financial-summary"),
            "synthetic_analytics", 5432, new string('x', 64)), protection);
        Assert.Throws<ArgumentException>(() => new AnalyticsPersistenceContext(wrong));
        await using var database = new OwnedPostgresStore(new OwnedPostgresOptions(new StorageIdentity("analytics"),
            "synthetic_analytics", 5432, new string('x', 64)), protection);
        var context = new AnalyticsPersistenceContext(database);
        var store = new PostgresAnalyticsReadModelStore(context);
        await Assert.ThrowsAsync<NotSupportedException>(() => store.ResetAsync(default));
        await Assert.ThrowsAsync<NotSupportedException>(() => store.MarkPublicationCompletedAsync("synthetic-event", Owner, "USD", default));
        await Assert.ThrowsAsync<StorageUnavailableException>(() => new PostgresAnalyticsEventPublisher(context).PublishAsync(Publication(), default));
        var request = new FinancialAssistant.Analytics.Contracts.AnalyticsRebuildRequest(Owner, Projection().Date,
            Projection().Date, "synthetic-source-v1", Time);
        var digest = PostgresAnalyticsReadModelStore.CalculateSourceDigest(new[] { Projection() });
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => store.BeginRebuildAsync(request with { PeriodEnd = request.PeriodStart.AddDays(3650) }, 1, digest));
        await Assert.ThrowsAsync<ArgumentException>(() => store.BeginRebuildAsync(request, 1001, digest));
        await Assert.ThrowsAsync<ArgumentException>(() => store.BeginRebuildAsync(request, 1, "invalid"));
        await Assert.ThrowsAsync<ArgumentException>(() => store.BeginRebuildAsync(request, 0, digest));
    }

    [Fact]
    public void PublicationValidatesStableIdentityAndDeterministicValues()
    {
        var valid = Publication();
        PostgresAnalyticsEventPublisher.Validate(valid);
        foreach (var invalid in new[]
        {
            valid.Payload with { MonthlyIncomeTotal = -1 }, valid.Payload with { DailyExpenseLimit = 0 },
            valid.Payload with { DailyExpenseSpent = 11 }, valid.Payload with { UncategorizedExpenseTotal = 11 },
            valid.Payload with { TopExpenseCategoryAmount = 11 }, valid.Payload with { TopExpenseCategoryId = null },
            valid.Payload with { ReferenceDate = valid.Payload.ReferenceDate.AddDays(1) }
        })
        {
            var bad = new IntegrationEventEnvelope<AnalyticsUpdatedV1>(valid.EventId, valid.OccurrenceId, valid.EventType,
                valid.OccurredAtUtc, valid.Producer, 1, valid.CorrelationId, valid.CausationId, Owner, invalid);
            Assert.Throws<ArgumentException>(() => PostgresAnalyticsEventPublisher.Validate(bad));
        }
    }

    private static IntegrationEventEnvelope<FinancialRecordChangedV1> Source(AnalyticsRecordProjection value) => new(value.EventId,
        value.EventId, value.RecordType + ".created.v1", value.ChangedAtUtc, "synthetic-financial-producer", 1,
        "synthetic-correlation", "synthetic-cause", value.UserIdHash,
        new FinancialRecordChangedV1(value.RecordId, value.Amount, value.Currency, value.CategoryId, value.Date,
            value.Status, value.Revision, "manual", value.ChangedAtUtc));
    private static IntegrationEventEnvelope<AnalyticsUpdatedV1> Publication()
    {
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("analytics-updated|synthetic-source|USD"))).ToLowerInvariant()[..32];
        return new(id, id, AnalyticsEventTypes.AnalyticsUpdated, Time, "financial-assistant-analytics-service", 1,
            "synthetic-correlation", "synthetic-source", Owner,
            new AnalyticsUpdatedV1("USD", new DateOnly(2026, 10, 20), 20m, 10m, 50m, 10m, "expense.synthetic", Time, 10m, 0));
    }
    private sealed class RecordingTransaction : IAnalyticsProjectionTransaction
    {
        public int Executions { get; private set; }
        public bool Completed { get; private set; }
        public async Task ExecuteAsync(IntegrationEventEnvelope<FinancialRecordChangedV1> source, Func<CancellationToken, Task> apply, CancellationToken cancellationToken)
        { Executions++; await apply(cancellationToken); Completed = true; }
    }
    private sealed class StagingPublisher : IAnalyticsEventPublisher
    {
        public bool ConfirmsDelivery => false;
        public int Staged { get; private set; }
        public Task PublishAsync(IntegrationEventEnvelope<AnalyticsUpdatedV1> envelope, CancellationToken cancellationToken)
        { Staged++; return Task.CompletedTask; }
    }
}
