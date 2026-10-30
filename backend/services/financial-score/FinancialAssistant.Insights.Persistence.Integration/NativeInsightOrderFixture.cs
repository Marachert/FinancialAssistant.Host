using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.RecommendationsNotifications.Infrastructure.Persistence;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

internal static class NativeInsightOrderFixture
{
    internal static string Gate { get; private set; } = "validation";
    private static readonly DateTimeOffset At = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    internal static async Task VerifyAsync(NativePostgresSettings settings, string mode)
    {
        using var protection = settings.CreateProtection();
        await using var database = new OwnedPostgresStore(settings.CreateOptions("recommendations-notifications"), protection);
        var context = new InsightPersistenceContext(database, requireNativeOrder: true);
        var clock = new Clock { Now = At.AddHours(mode == "initial" ? 1 : 4) };
        var store = new PostgresRecommendationNotificationStore(context);
        var profile = new PostgresRecommendationProfileSettingsProvider(context, clock);
        var preferences = new PostgresNotificationPreferenceProvider(context);
        var publisher = new PostgresInsightEventPublisher(context);
        var service = Service(publisher);
        var owner = new string('e', 64);
        var first = Source(owner, "synthetic-native-first", At.AddMinutes(10));
        var second = Source(owner, "synthetic-z-native-second", At);
        var third = Source(owner, "synthetic-a-native-third", At);
        var stale = Source(owner, "synthetic-native-stale", At.AddMinutes(20));
        if (mode == "initial")
        {
            Gate = "preferences bootstrap and ordered financial facts";
            await Unavailable(() => service.ProcessAnalyticsAsync(first, default));
            await profile.ApplySnapshotAsync(new RecommendationProfileSnapshot(owner, "USD", "synthetic-native-profile", 1, false, 1000m, clock.Now));
            await preferences.EnsureDisabledIfMissingAsync(owner);
            var defaults = await preferences.GetAsync(owner, default);
            Check(!defaults.PushEnabled && !defaults.WebEnabled);
            Check((await Ordered(10, first)).Count > 0);
            Check((await Ordered(20, second)).Count > 0);
            await Current(second.EventId);
            Check((await Ordered(15, stale)).Count == 0);
            await Current(second.EventId);
            await Conflict(() => Ordered(20, Source(owner, "synthetic-reused-order", At)));
            await Conflict(() => Ordered(21, second));
            Check((await OrderedScore(10, Score(owner, "synthetic-native-score-first", At.AddMinutes(10)))).Count > 0);
            var secondScore = Score(owner, "synthetic-z-native-score-second", At);
            Check((await OrderedScore(20, secondScore)).Count > 0);
            await Current(secondScore.EventId);
            var thirdScore = Score(owner, "synthetic-a-native-score-third", At);
            Check((await OrderedScore(21, thirdScore)).Count > 0);
            await Current(thirdScore.EventId);
            Check((await OrderedScore(15, Score(owner, "synthetic-native-stale-score", At.AddMinutes(20)))).Count == 0);
            await Current(thirdScore.EventId);
            var broken = Service(new MissingPublisher());
            await Unavailable(() => context.ExecuteNativeOrderedAsync("analytics", 21, token => broken.ProcessAnalyticsAsync(third, token)));
            Check((await Ordered(21, third)).Count > 0);
            await Current(third.EventId);
            await preferences.UpdateAsync(owner, new NotificationPreferences(false, true), default);
            Gate = "preserve explicit preferences";
            await preferences.EnsureDisabledIfMissingAsync(owner);
            Check((await preferences.GetAsync(owner, default)).WebEnabled);
            var preparation = new NotificationPreparationService(store, new NotificationTemplateCatalog(), publisher, preferences, context);
            Gate = "bounded owned preparation outbox";
            var output = new List<RecommendationOutboxMessage>();
            for (var page = 0; page < 8; page++)
            {
                var batch = await publisher.ReadRecommendationsAsync(owner);
                if (batch.Count == 0) { break; }
                Check(batch.Count <= 25);
                foreach (var message in batch)
                {
                    output.Add(message);
                    Check(await publisher.MarkPublishedAsync(message, clock.Now));
                }
            }
            Check(output.Count > 25 && output.Select(message => message.Event.EventId).Distinct().Count() == output.Count);
            Check((await publisher.ReadRecommendationsAsync(owner)).Count == 0);
            var currentOutput = output.First(message => message.Event.CausationId == third.EventId);
            Gate = "current owned recommendation preparation";
            Check((await preparation.ProcessAsync(currentOutput.Event, default)).Single().Channel == "web");
            var staleOutput = output.First(message => message.Event.CausationId == first.EventId);
            Gate = "stale owned recommendation suppression";
            var notificationCount = (await store.GetNotificationsAsync(owner, "USD", default)).Count;
            Check((await preparation.ProcessAsync(staleOutput.Event, default)).Count == 0);
            Check((await store.GetNotificationsAsync(owner, "USD", default)).Count == notificationCount);
            var triggers = new NotificationTriggerService(store, new NotificationTriggerEvaluator(), new NotificationTemplateCatalog(), publisher, preferences, context);
            Gate = "independent trigger streams";
            var analyticsTrigger = Trigger(owner, "synthetic-native-analytics-trigger");
            var scoreTrigger = Trigger(owner, "synthetic-native-score-trigger");
            await context.ExecuteNativeOrderedAsync("analytics", 10, token => triggers.ProcessAsync(analyticsTrigger, token));
            await context.ExecuteNativeOrderedAsync("score", 10, token => triggers.ProcessAsync(scoreTrigger, token));
            var before = (await store.GetNotificationsAsync(owner, "USD", default)).Count;
            Check((await context.ExecuteNativeOrderedAsync("analytics", 9, token => triggers.ProcessAsync(
                Trigger(owner, "synthetic-native-stale-trigger"), token))).Count == 0);
            Check((await store.GetNotificationsAsync(owner, "USD", default)).Count == before);
            await database.ExecuteAsync([], async (records, _) =>
            {
                Check(await records.GetAsync("insight_order_heads", owner, "trigger:analytics:USD") is not null);
                Check(await records.GetAsync("insight_order_heads", owner, "trigger:score:USD") is not null);
                return true;
            });
        }
        else
        {
            Gate = "ordered restart and freshness rollback";
            await Current(third.EventId);
            Check((await Ordered(21, third)).Count > 0);
            Check((await Ordered(15, stale)).Count == 0);
            Check((await OrderedScore(19, Score(owner, "synthetic-native-stale-score-restart", At.AddMinutes(20)))).Count == 0);
            var fresh = Source(owner, "synthetic-native-after-restart", At);
            await Unavailable(() => Ordered(22, fresh));
            await profile.ApplySnapshotAsync(new RecommendationProfileSnapshot(owner, "USD", "synthetic-native-profile", 1, false, 1000m, clock.Now));
            Check((await Ordered(22, fresh)).Count > 0);
            await Current(fresh.EventId);
        }
        Console.WriteLine("PASS native insight order mode=" + mode + ": backdated/equal-time inputs, stale suppression, ordinal conflicts, rollback, independent trigger streams and durable replay. No broker delivery claimed.");

        RecommendationService Service(IRecommendationEventPublisher output) => new(store, new RecommendationGenerator(),
            new DeterministicRecommendationWordingProvider(), output, profile, transaction: context);
        Task<IReadOnlyList<FinancialRecommendation>> Ordered(long sequence, IntegrationEventEnvelope<AnalyticsUpdatedV1> source) =>
            context.ExecuteNativeOrderedAsync("analytics", sequence, token => service.ProcessAnalyticsAsync(source, token));
        Task<IReadOnlyList<FinancialRecommendation>> OrderedScore(long sequence, IntegrationEventEnvelope<ScoreCalculatedV1> source) =>
            context.ExecuteNativeOrderedAsync("score", sequence, token => service.ProcessScoreAsync(source, token));
        async Task Current(string id)
        {
            var values = (await store.GetRecommendationsAsync(owner, "USD", default)).Where(r => r.Status == "active").ToArray();
            Check(values.Length > 0 && values.All(r => r.SourceEventId == id));
        }
    }

    private static IntegrationEventEnvelope<AnalyticsUpdatedV1> Source(string owner, string id, DateTimeOffset at) =>
        new(id, id + "-occurrence", AnalyticsEventTypes.AnalyticsUpdated, at, "financial-assistant-analytics-service", 1,
            "synthetic-correlation", id + "-cause", owner,
            new AnalyticsUpdatedV1("USD", DateOnly.FromDateTime(at.UtcDateTime), 1000m, 900m, 100m, 200m, "expense.food", at, 400m, 0m));
    private static NotificationTriggerFacts Trigger(string owner, string id) =>
        new(owner, "USD", DateOnly.FromDateTime(At.UtcDateTime), false, 1000m, 1100m, 60, 80, true, true, id, "synthetic-correlation", At);
    private static IntegrationEventEnvelope<ScoreCalculatedV1> Score(string owner, string id, DateTimeOffset at) =>
        new(id, id + "-occurrence", FinancialScoreEventTypes.ScoreCalculated, at, "financial-score-service", 1,
            "synthetic-correlation", id + "-cause", owner,
            new ScoreCalculatedV1(id + "-calculation", "USD", 80, "financial-score-v2",
                new[] { "budget_usage", "spending_trend", "income_consistency", "data_completeness", "penalty_cap" }
                    .Select(code => new FinancialScoreFactorV1(code, 0m)).ToArray(), at));
    private static void Check(bool value) { if (!value) { throw new FixtureFailure(); } }
    private static async Task Conflict(Func<Task> operation) { try { await operation(); throw new FixtureFailure(); } catch (StorageConflictException) { } }
    private static async Task Unavailable(Func<Task> operation) { try { await operation(); throw new FixtureFailure(); } catch (StorageUnavailableException) { } }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class MissingPublisher : IRecommendationEventPublisher
    { public Task PublishAsync(IntegrationEventEnvelope<RecommendationGeneratedV1> source, CancellationToken token) => Task.CompletedTask; }
}
