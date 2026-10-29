using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.RecommendationsNotifications.Infrastructure.Persistence;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;
using Npgsql;

internal static class RecommendationFixture
{
    private static readonly DateTimeOffset At = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    public static async Task VerifyAsync(NativePostgresSettings settings, NpgsqlConnection admin, string mode)
    {
        using var protection = settings.CreateProtection();
        await using var database = new OwnedPostgresStore(settings.CreateOptions("recommendations-notifications"), protection);
        await using var otherDatabase = new OwnedPostgresStore(settings.CreateOptions("recommendations-notifications"), protection);
        Check(await database.IsReadyAsync());
        var context = new InsightPersistenceContext(database);
        var secondContext = new InsightPersistenceContext(otherDatabase);
        var clock = new Clock { Now = At.AddHours(mode == "initial" ? 1 : 4) };
        var store = new PostgresRecommendationNotificationStore(context);
        var secondStore = new PostgresRecommendationNotificationStore(secondContext);
        var profile = new PostgresRecommendationProfileSettingsProvider(context, clock);
        var preferences = new PostgresNotificationPreferenceProvider(context);
        var secondPreferences = new PostgresNotificationPreferenceProvider(secondContext);
        var publisher = new PostgresInsightEventPublisher(context);
        var service = Recommendations(store, publisher, profile, context);
        var second = Recommendations(secondStore, new PostgresInsightEventPublisher(secondContext),
            new PostgresRecommendationProfileSettingsProvider(secondContext, clock), secondContext);
        var notifications = new NotificationPreparationService(store, new NotificationTemplateCatalog(), publisher, preferences, context);
        var triggers = new NotificationTriggerService(store, new NotificationTriggerEvaluator(), new NotificationTemplateCatalog(), publisher, preferences, context);
        var owner = new string('c', 64);
        var other = new string('d', 64);
        var source = Analytics(owner, "synthetic-insight-analytics", At);
        if (mode == "initial")
        {
            await Unavailable(() => service.ProcessAnalyticsAsync(source, default));
            Check((await store.GetRecommendationsAsync(owner, "USD", default)).Count == 0);
            await Unavailable(() => preferences.GetAsync(owner, default));
            Check(await profile.ApplySnapshotAsync(new RecommendationProfileSnapshot(owner, "USD", "synthetic-profile-v1", 1, true, 1000m, At)));
            Check(await profile.ApplySnapshotAsync(new RecommendationProfileSnapshot(other, "USD", "synthetic-profile-v1", 1, false, null, At)));
            await preferences.UpdateAsync(owner, new NotificationPreferences(true, true), default);
            await preferences.UpdateAsync(other, new NotificationPreferences(false, false), default);
            var concurrent = await Task.WhenAll(service.ProcessAnalyticsAsync(source, default), second.ProcessAnalyticsAsync(source, default));
            Check(concurrent[0].Count > 0 && concurrent[0].Select(r => r.RecommendationId).SequenceEqual(concurrent[1].Select(r => r.RecommendationId)));
            var firstEvents = await publisher.ReadRecommendationsAsync(owner);
            Check(firstEvents.Count == concurrent[0].Count);
            foreach (var message in firstEvents) { Check((await notifications.ProcessAsync(message.Event, default)).Count == 2); }
            var firstNotifications = await publisher.ReadNotificationsAsync(owner);
            Check(firstNotifications.Count == firstEvents.Count * 2 && !publisher.ConfirmsDelivery);
            var first = firstEvents[0];
            var notification = firstNotifications[0];
            await Task.WhenAll(store.UpdateRecommendationStatusAsync(owner, first.Event.Payload.RecommendationId, "read", At.AddMinutes(1), default),
                secondPreferences.UpdateAsync(owner, new NotificationPreferences(false, true,
                    new NotificationQuietHours(new TimeOnly(22, 0), new TimeOnly(7, 0), "UTC"), [NotificationTriggerCodes.DailyInputReminder]), default));
            Check((await store.GetRecommendationsAsync(owner, "USD", default)).Single(r => r.RecommendationId == first.Event.Payload.RecommendationId).Status == "read");
            Check(!(await preferences.GetAsync(owner, default)).PushEnabled);
            await store.UpdateRecommendationStatusAsync(owner, first.Event.Payload.RecommendationId, "dismissed", At.AddMinutes(2), default);
            await store.MarkNotificationReadAsync(owner, notification.Event.Payload.NotificationId, At.AddMinutes(2), default);
            await store.MarkNotificationReadAsync(owner, notification.Event.Payload.NotificationId, At.AddMinutes(3), default);
            Check((await store.GetNotificationsAsync(owner, "USD", default)).Single(n => n.NotificationId == notification.Event.Payload.NotificationId).ReadAtUtc == At.AddMinutes(2));
            await Rejected(() => store.UpdateNotificationStatusAsync(owner, notification.Event.Payload.NotificationId, "delivered", At.AddMinutes(3), default));
            await Unavailable(() => store.MarkNotificationPublishedAsync(notification.Event.Payload.NotificationId, default));
            var attempt = new NotificationDeliveryAttempt(notification.Event.Payload.NotificationId, notification.Event.Payload.Channel, 1,
                "retry-scheduled", true, "provider-not-configured", At.AddMinutes(3), At.AddMinutes(4));
            await store.RecordUndeliveredAttemptAsync(owner, attempt);
            await secondStore.RecordUndeliveredAttemptAsync(owner, attempt);
            await Conflict(() => store.RecordUndeliveredAttemptAsync(owner, attempt with { FailureCode = "provider-unavailable" }));
            await store.RecordUndeliveredAttemptAsync(owner, attempt with
            { AttemptNumber = 2, Status = "failed", IsRetryable = false, AttemptedAtUtc = At.AddMinutes(4), RetryAtUtc = null });
            Check(await publisher.MarkPublishedAsync(first, At.AddHours(1)));
            Check(!await publisher.MarkPublishedAsync(first, At.AddHours(2)));
            Check(await publisher.MarkPublishedAsync(notification, At.AddHours(1)));
            Check(!await publisher.MarkPublishedAsync(notification, At.AddHours(2)));
            var fake = new IntegrationEventEnvelope<RecommendationGeneratedV1>(first.Event.EventId, first.Event.OccurrenceId, first.Event.EventType,
                first.Event.OccurredAtUtc, first.Event.Producer, 1, first.Event.CorrelationId, first.Event.CausationId, owner, first.Event.Payload with { Title = "Synthetic forged title" });
            await Conflict(() => publisher.MarkPublishedAsync(new RecommendationOutboxMessage(fake, null), At.AddHours(1)));
            await Conflict(() => service.ProcessAnalyticsAsync(Copy(source, source.EventId, source.Payload with { MonthlyExpenseTotal = 999m }), default));
            await Conflict(() => service.ProcessAnalyticsAsync(Copy(source, source.EventId, source.Payload, other), default));
            var stale = Analytics(owner, "synthetic-insight-stale", At.AddMinutes(-1));
            Check((await service.ProcessAnalyticsAsync(stale, default)).Count == 0);
            var next = Analytics(owner, "synthetic-insight-next", At.AddMinutes(5));
            await service.ProcessAnalyticsAsync(next, default);
            Check((await store.GetRecommendationsAsync(owner, "USD", default)).Single(r => r.RecommendationId == first.Event.Payload.RecommendationId).Status == "dismissed");
            var nextEvents = (await publisher.ReadRecommendationsAsync(owner)).Where(m => m.Event.CausationId == next.EventId).ToArray();
            foreach (var message in nextEvents)
            { Check((await notifications.ProcessAsync(message.Event, default)).Single().Channel == "web"); }
            var beforeReplay = (await store.GetNotificationsAsync(owner, "USD", default)).Count;
            Check((await notifications.ProcessAsync(first.Event, default)).Count == 2);
            Check((await store.GetNotificationsAsync(owner, "USD", default)).Count == beforeReplay);
            var noDelivery = await service.ProcessAnalyticsAsync(Analytics(other, "synthetic-insight-other", At), default);
            Check(noDelivery.Count > 0);
            foreach (var message in await publisher.ReadRecommendationsAsync(other)) { Check((await notifications.ProcessAsync(message.Event, default)).Count == 0); }
            Check((await store.GetNotificationsAsync(other, "USD", default)).Count == 0);
            var savedPreferences = await preferences.GetAsync(owner, default);
            await preferences.UpdateAsync(owner, savedPreferences with { PushEnabled = true }, default);
            var trigger = Trigger(owner, "synthetic-trigger-0", At.AddMinutes(6));
            Check((await triggers.ProcessAsync(trigger, default)).Count == 8);
            for (var i = 1; i < 4; i++)
            {
                var later = Trigger(owner, "synthetic-trigger-" + i, At.AddMinutes(6 + i));
                // Daily budget and same-score notifications already exist; only event-specific receipt/recommendation intents are new.
                Check((await triggers.ProcessAsync(later, default)).Count == 4);
            }
            var countBeforeDedup = (await store.GetNotificationsAsync(owner, "USD", default)).Count;
            Check((await triggers.ProcessAsync(Trigger(owner, "synthetic-trigger-dedup", At.AddMinutes(11)) with
            { RecommendationAvailable = false, ReceiptProcessingCompleted = false }, default)).Count == 0);
            Check((await store.GetNotificationsAsync(owner, "USD", default)).Count == countBeforeDedup);
            await preferences.UpdateAsync(owner, savedPreferences, default);
            Check((await triggers.ProcessAsync(trigger, default)).Count == 8);
            await Conflict(() => triggers.ProcessAsync(trigger with { ConfirmedBudgetSpend = 1200m }, default));
            Check((await publisher.ReadNotificationsAsync(owner)).Count == 25);
            Check((await service.ProcessScoreAsync(Score(owner), default)).Count > 0);
            var failed = Analytics(owner, "synthetic-insight-failed", At.AddMinutes(10));
            var broken = Recommendations(store, new MissingPublisher(), profile, context);
            await Unavailable(() => broken.ProcessAnalyticsAsync(failed, default));
            await NoClaim(database, failed.EventId);
            await Sql(admin, """
                CREATE FUNCTION fa_recommendations_notifications.synthetic_abort() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN IF NEW.bucket='insight_state' THEN RAISE EXCEPTION 'synthetic rollback'; END IF; RETURN NEW; END $$;
                CREATE TRIGGER synthetic_abort BEFORE INSERT OR UPDATE ON fa_recommendations_notifications.records
                  FOR EACH ROW EXECUTE FUNCTION fa_recommendations_notifications.synthetic_abort();
                """);
            try { await Unavailable(() => service.ProcessAnalyticsAsync(failed, default)); }
            finally { await Sql(admin, "DROP TRIGGER synthetic_abort ON fa_recommendations_notifications.records; DROP FUNCTION fa_recommendations_notifications.synthetic_abort()"); }
            await NoClaim(database, failed.EventId);
            Check(!(await store.GetRecommendationsAsync(owner, "USD", default)).Any(r => r.SourceEventId == failed.EventId));
        }
        if (mode == "restart") { await Unavailable(() => profile.GetAsync(owner, "USD", default)); }
        var before = (await store.GetRecommendationsAsync(owner, "USD", default)).Count;
        var replay = await service.ProcessAnalyticsAsync(source, default);
        Check(replay.Count > 0 && (await store.GetRecommendationsAsync(owner, "USD", default)).Count == before);
        var completed = (await store.GetRecommendationsAsync(owner, "USD", default)).First(r => r.Status == "dismissed");
        Check(replay.Any(r => r.RecommendationId == completed.RecommendationId && r.Status == "dismissed"));
        var scoreReplay = await service.ProcessScoreAsync(Score(owner), default);
        Check(scoreReplay.Any(r => r.Code == "score-strength"));
        Check((await triggers.ProcessAsync(Trigger(owner, "synthetic-trigger-0", At.AddMinutes(6)), default)).Count == 8);
        Check((await publisher.ReadNotificationsAsync(owner)).Count == 25);
        var values = await store.GetNotificationsAsync(owner, "USD", default);
        Check(values.Any(n => n.DeliveryStatus == "failed" && n.ReadAtUtc == At.AddMinutes(2)));
        Check(values.All(n => n.DeliveryStatus != "delivered"));
        var prefs = await preferences.GetAsync(owner, default);
        Check(!prefs.PushEnabled && prefs.WebEnabled && prefs.QuietHours?.TimeZoneId == "UTC");
        Check((await store.GetNotificationsAsync(other, "USD", default)).Count == 0);
        Check((await store.GetRecommendationsAsync(other, "EUR", default)).Count == 0);
        Check(await store.UpdateRecommendationStatusAsync(other, completed.RecommendationId, "read", At.AddHours(1), default) is null);
        var owners = await context.ReadOwnerPageAsync(pageSize: 1);
        Check(owners.Owners.Count == 1 && owners.NextCursor is not null);
        Check((await context.ReadOwnerPageAsync(owners.NextCursor)).Owners.Count == 1);
        if (mode == "restart")
        {
            var expired = Analytics(owner, "synthetic-insight-expired", At.AddMinutes(20));
            await Unavailable(() => service.ProcessAnalyticsAsync(expired, default));
            await NoClaim(database, expired.EventId);
        }
    }

    private static RecommendationService Recommendations(IRecommendationNotificationStore store, IRecommendationEventPublisher publisher,
        IRecommendationProfileSettingsProvider profile, IInsightProcessingTransaction transaction) =>
        new(store, new RecommendationGenerator(), new DeterministicRecommendationWordingProvider(), publisher, profile, transaction: transaction);
    private static IntegrationEventEnvelope<AnalyticsUpdatedV1> Analytics(string owner, string id, DateTimeOffset at) =>
        new(id, id + "-occurrence", AnalyticsEventTypes.AnalyticsUpdated, at, "synthetic-analytics", 1, "synthetic-correlation", id + "-cause", owner,
            new AnalyticsUpdatedV1("USD", DateOnly.FromDateTime(at.UtcDateTime), 1000m, 900m, 100m, 200m, "expense.food", at, 400m, 0m));
    private static IntegrationEventEnvelope<AnalyticsUpdatedV1> Copy(IntegrationEventEnvelope<AnalyticsUpdatedV1> source, string id,
        AnalyticsUpdatedV1 payload, string? owner = null) => new(id, source.OccurrenceId, source.EventType, source.OccurredAtUtc,
            source.Producer, 1, source.CorrelationId, source.CausationId, owner ?? source.UserIdHash, payload);
    private static NotificationTriggerFacts Trigger(string owner, string id, DateTimeOffset at) =>
        new(owner, "USD", DateOnly.FromDateTime(at.UtcDateTime), false, 1000m, 1100m, 60, 80, true, true, id, "synthetic-correlation", at);
    private static IntegrationEventEnvelope<ScoreCalculatedV1> Score(string owner) =>
        new("synthetic-insight-score", "synthetic-score-occurrence", FinancialScoreEventTypes.ScoreCalculated, At.AddMinutes(12),
            "synthetic-score-service", 1, "synthetic-correlation", "synthetic-score-cause", owner,
            new ScoreCalculatedV1("synthetic-score-calculation", "USD", 80, "financial-score-v2",
                new[] { "budget_usage", "spending_trend", "income_consistency", "data_completeness", "penalty_cap" }
                    .Select(code => new FinancialScoreFactorV1(code, 0m)).ToArray(), At.AddMinutes(12)));
    private static async Task NoClaim(OwnedPostgresStore database, string id) => Check(await database.ExecuteAsync([], async (records, _) =>
        await records.GetAsync("insight_claims", "recommendations-notifications", "analytics:" + id) is null));
    private static void Check(bool condition) { if (!condition) { throw new FixtureFailure(); } }
    private static async Task Conflict(Func<Task> action) { try { await action(); throw new FixtureFailure(); } catch (StorageConflictException) { } }
    private static async Task Unavailable(Func<Task> action) { try { await action(); throw new FixtureFailure(); } catch (StorageUnavailableException) { } }
    private static async Task Rejected(Func<Task> action) { try { await action(); throw new FixtureFailure(); } catch (InvalidOperationException) { } }
    private static async Task Sql(NpgsqlConnection connection, string sql) { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now { get; set; } public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class MissingPublisher : IRecommendationEventPublisher
    { public Task PublishAsync(IntegrationEventEnvelope<RecommendationGeneratedV1> envelope, CancellationToken cancellationToken) => Task.CompletedTask; }
}
