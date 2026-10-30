using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.RecommendationsNotifications.Infrastructure.Persistence;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

internal static class NativeInsightOutboxFixture
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    internal static async Task VerifyAsync(NativePostgresSettings settings, string mode)
    {
        using var protection = settings.CreateProtection();
        await using var database = new OwnedPostgresStore(settings.CreateOptions("recommendations-notifications"), protection);
        var context = new InsightPersistenceContext(database, requireNativeOrder: true);
        var store = new PostgresRecommendationNotificationStore(context);
        var publisher = new PostgresInsightEventPublisher(context);
        var profile = new PostgresRecommendationProfileSettingsProvider(context, new Clock());
        var service = new RecommendationService(store, new RecommendationGenerator(), new DeterministicRecommendationWordingProvider(),
            publisher, profile, transaction: context);
        var owners = Enumerable.Range(0, 31).Select(index => Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes("synthetic-native-insight-scan-" + index))).ToLowerInvariant()).ToArray();
        if (mode == "initial")
        {
            for (var index = 0; index < owners.Length; index++)
            {
                await profile.ApplySnapshotAsync(new RecommendationProfileSnapshot(owners[index], "USD", "synthetic-scan-profile", 1, false, null, At));
                var id = "synthetic-native-insight-scan-" + index;
                var source = new IntegrationEventEnvelope<AnalyticsUpdatedV1>(id, id + "-occurrence", AnalyticsEventTypes.AnalyticsUpdated,
                    At, "financial-assistant-analytics-service", 1, "synthetic-correlation", id + "-cause", owners[index],
                    new AnalyticsUpdatedV1("USD", DateOnly.FromDateTime(At.UtcDateTime), 1000m, 900m, 100m, 200m, "expense.food", At, 400m, 0m));
                Check((await context.ExecuteNativeOrderedAsync("analytics", 1, token => service.ProcessAnalyticsAsync(source, token))).Count > 0);
            }
        }
        else
        {
            Check(await database.ExecuteAsync([], async (records, _) =>
                await records.GetAsync("insight_outbox_scan", "recommendations-notifications", "recommendations") is not null));
        }
        var legacyOwner = new string('c', 64);
        var before = (await publisher.ReadRecommendationsAsync(legacyOwner)).Select(message => message.Event.EventId).ToArray();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var pass = 0; pass < 16; pass++)
        {
            var batch = await publisher.ReadRecommendationsBatchAsync();
            Check(batch.Count <= 25 && batch.Select(message => message.Event.EventId).Distinct().Count() == batch.Count);
            foreach (var message in batch.Where(message => owners.Contains(message.Event.UserIdHash, StringComparer.Ordinal)))
            {
                seen.Add(message.Event.UserIdHash!);
                if (mode == "restart") { Check(await publisher.MarkPublishedAsync(message, At.AddHours(4))); }
            }
            if (mode == "initial" && seen.Count == owners.Length) { break; }
        }
        Check(seen.Count == owners.Length);
        foreach (var owner in owners)
        {
            var pending = await publisher.ReadRecommendationsAsync(owner);
            Check(mode == "initial" ? pending.Count > 0 : pending.Count == 0);
        }
        Check((await publisher.ReadRecommendationsAsync(legacyOwner)).Select(message => message.Event.EventId).SequenceEqual(before));
        Console.WriteLine("PASS native recommendation outbox mode=" + mode + ": 31-owner bounded discovery, durable cursor/restart and exact synthetic acknowledgements; no transport/provider delivery claimed.");
    }
    private static void Check(bool value) { if (!value) { throw new FixtureFailure(); } }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => At.AddHours(1); }
}
