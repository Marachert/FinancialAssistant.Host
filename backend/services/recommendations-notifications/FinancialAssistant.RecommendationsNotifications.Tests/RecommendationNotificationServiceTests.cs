using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.RecommendationsNotifications.Infrastructure;
using FinancialAssistant.Shared.Contracts.Events;
using Xunit;

namespace FinancialAssistant.RecommendationsNotifications.Tests;

public sealed class RecommendationNotificationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AnalyticsEvent_GeneratesRecommendationsAndPreparesBothChannels()
    {
        var fixture = new ServiceFixture();
        var envelope = AnalyticsEnvelope("analytics-1", "owner-a", 1_000m, 900m);

        var recommendations = await fixture.Recommendations.ProcessAnalyticsAsync(
            envelope,
            CancellationToken.None);
        var notifications = await fixture.Notifications.GetAsync(
            "owner-a",
            "USD",
            CancellationToken.None);

        Assert.Contains(recommendations, item => item.Code == "spending-pressure");
        Assert.Equal(
            new[] { NotificationChannels.Push, NotificationChannels.Web },
            notifications.Select(item => item.Channel).OrderBy(item => item));
        Assert.All(
            notifications,
            item => Assert.Equal(NotificationDeliveryStatuses.Prepared, item.DeliveryStatus));
        Assert.Equal(notifications.Count, fixture.NotificationPublisher.Published.Count);
    }

    [Fact]
    public async Task NotificationPreferences_FilterChannelsBeforePreparation()
    {
        var fixture = new ServiceFixture();
        fixture.NotificationPreferences.Set(
            "owner-a",
            new NotificationPreferences(false, true));

        var generated = await fixture.Recommendations.ProcessAnalyticsAsync(
            AnalyticsEnvelope("analytics-web-only", "owner-a", 1_000m, 900m),
            CancellationToken.None);
        var notifications = await fixture.Notifications.GetAsync(
            "owner-a",
            "USD",
            CancellationToken.None);

        Assert.NotEmpty(generated);
        Assert.NotEmpty(notifications);
        Assert.All(
            notifications,
            item => Assert.Equal(NotificationChannels.Web, item.Channel));
        Assert.Equal(notifications.Count, fixture.NotificationPublisher.Published.Count);

        fixture.NotificationPreferences.Set(
            "owner-b",
            new NotificationPreferences(false, false));
        var suppressed = await fixture.Recommendations.ProcessAnalyticsAsync(
            AnalyticsEnvelope("analytics-suppressed", "owner-b", 1_000m, 900m),
            CancellationToken.None);
        var suppressedNotifications = await fixture.Notifications.GetAsync(
            "owner-b",
            "USD",
            CancellationToken.None);

        Assert.NotEmpty(suppressed);
        Assert.Empty(suppressedNotifications);
    }

    [Fact]
    public async Task TriggerFacts_PrepareAllApplicableSafeNotifications()
    {
        var fixture = new ServiceFixture();
        var notifications = await fixture.Triggers.ProcessAsync(
            new NotificationTriggerFacts(
                "owner-a",
                "USD",
                new DateOnly(2026, 8, 2),
                false,
                1_000m,
                1_100m,
                60,
                70,
                true,
                true,
                "trigger-source-1",
                "correlation-1",
                Now),
            CancellationToken.None);

        Assert.Equal(10, notifications.Count);
        Assert.Equal(
            5,
            notifications
                .Select(item => item.TemplateCode)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.DoesNotContain(
            notifications,
            item => item.TemplateCode.Contains(
                NotificationTriggerCodes.BudgetApproaching,
                StringComparison.Ordinal));
        Assert.All(
            notifications,
            item =>
            {
                Assert.DoesNotContain("1000", item.Body);
                Assert.DoesNotContain("1100", item.Body);
                Assert.DoesNotContain("owner-a", item.Body);
                Assert.DoesNotContain("trigger-source-1", item.Body);
            });
    }

    [Fact]
    public async Task TriggerFacts_RespectPreferencesAndDeduplicateOccurrences()
    {
        var fixture = new ServiceFixture();
        fixture.NotificationPreferences.Set(
            "owner-a",
            new NotificationPreferences(false, true));
        var facts = new NotificationTriggerFacts(
            "owner-a",
            "USD",
            new DateOnly(2026, 8, 2),
            true,
            1_000m,
            850m,
            null,
            null,
            false,
            false,
            "trigger-source-2",
            "correlation-2",
            Now);

        var first = await fixture.Triggers.ProcessAsync(
            facts,
            CancellationToken.None);
        var replay = await fixture.Triggers.ProcessAsync(
            facts with { SourceEventId = "trigger-source-replay" },
            CancellationToken.None);

        var notification = Assert.Single(first);
        Assert.Equal(NotificationChannels.Web, notification.Channel);
        Assert.Contains(
            NotificationTriggerCodes.BudgetApproaching,
            notification.TemplateCode,
            StringComparison.Ordinal);
        Assert.Empty(replay);
    }

    [Fact]
    public async Task NotificationTypes_FilterTriggersAndRecommendationMessages()
    {
        var fixture = new ServiceFixture();
        var preferences = new NotificationPreferenceService(
            fixture.NotificationPreferences);
        await preferences.UpdateAsync(
            "owner-a",
            true,
            true,
            [NotificationTriggerCodes.BudgetExceeded],
            null,
            CancellationToken.None);

        var triggered = await fixture.Triggers.ProcessAsync(
            new NotificationTriggerFacts(
                "owner-a",
                "USD",
                new DateOnly(2026, 8, 2),
                false,
                1_000m,
                1_100m,
                60,
                70,
                true,
                true,
                "type-filter-source",
                "type-filter-correlation",
                Now),
            CancellationToken.None);
        var recommendations = await fixture.Recommendations.ProcessAnalyticsAsync(
            AnalyticsEnvelope(
                "analytics-type-filter",
                "owner-a",
                1_000m,
                900m),
            CancellationToken.None);
        var stored = await fixture.Notifications.GetAsync(
            "owner-a",
            "USD",
            CancellationToken.None);

        Assert.Equal(2, triggered.Count);
        Assert.All(
            triggered,
            item => Assert.Contains(
                NotificationTriggerCodes.BudgetExceeded,
                item.TemplateCode,
                StringComparison.Ordinal));
        Assert.NotEmpty(recommendations);
        Assert.Equal(triggered.Count, stored.Count);
        Assert.Equal(triggered.Count, fixture.NotificationPublisher.Published.Count);
    }

    [Fact]
    public async Task NotificationPreferences_KeepQuietHoursPlaceholder()
    {
        var fixture = new ServiceFixture();
        var quietHours = new NotificationQuietHours(
            new TimeOnly(22, 0),
            new TimeOnly(7, 0),
            "Etc/UTC");
        fixture.NotificationPreferences.Set(
            "owner-a",
            new NotificationPreferences(true, true, quietHours));

        var stored = await fixture.NotificationPreferences.GetAsync(
            "owner-a",
            CancellationToken.None);

        Assert.Equal(quietHours, stored.QuietHours);
    }

    [Fact]
    public async Task ReplayedEvent_IsIdempotent()
    {
        var fixture = new ServiceFixture();
        var envelope = AnalyticsEnvelope("analytics-replay", "owner-a", 1_000m, 900m);

        var first = await fixture.Recommendations.ProcessAnalyticsAsync(
            envelope,
            CancellationToken.None);
        var second = await fixture.Recommendations.ProcessAnalyticsAsync(
            envelope,
            CancellationToken.None);
        var notifications = await fixture.Notifications.GetAsync(
            "owner-a",
            "USD",
            CancellationToken.None);

        Assert.NotEmpty(first);
        Assert.Empty(second);
        Assert.Equal(first.Count * 2, notifications.Count);
    }

    [Fact]
    public async Task FailedNotificationPublication_IsRetriedWithoutLosingTheEvent()
    {
        var store = new InMemoryRecommendationNotificationStore();
        var notificationPublisher = new FailOnceNotificationPublisher();
        var notifications = new NotificationPreparationService(
            store,
            new NotificationTemplateCatalog(),
            notificationPublisher);
        var recommendations = new RecommendationService(
            store,
            new RecommendationGenerator(),
            new DeterministicRecommendationWordingProvider(),
            new InMemoryRecommendationEventPublisher(notifications));
        var envelope = AnalyticsEnvelope("analytics-retry", "owner-a", 1_000m, 900m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            recommendations.ProcessAnalyticsAsync(envelope, CancellationToken.None));

        var retried = await recommendations.ProcessAnalyticsAsync(
            envelope,
            CancellationToken.None);
        var replayed = await recommendations.ProcessAnalyticsAsync(
            envelope,
            CancellationToken.None);
        var prepared = await notifications.GetAsync(
            "owner-a",
            "USD",
            CancellationToken.None);

        Assert.NotEmpty(retried);
        Assert.Empty(replayed);
        Assert.Equal(2, prepared.Count);
        Assert.Equal(2, notificationPublisher.Published.Count);
    }

    [Fact]
    public async Task DelayedOlderAnalyticsEvent_DoesNotReplaceCurrentFacts()
    {
        var fixture = new ServiceFixture();
        await fixture.Recommendations.ProcessAnalyticsAsync(
            AnalyticsEnvelope(
                "analytics-newer",
                "owner-a",
                1_000m,
                900m,
                Now.AddMinutes(2)),
            CancellationToken.None);

        var delayed = await fixture.Recommendations.ProcessAnalyticsAsync(
            AnalyticsEnvelope(
                "analytics-older",
                "owner-a",
                1_000m,
                100m,
                Now),
            CancellationToken.None);
        var current = await fixture.Recommendations.GetAsync(
            "owner-a",
            "USD",
            CancellationToken.None);

        Assert.Empty(delayed);
        Assert.Contains(current, item => item.Code == "spending-pressure");
    }

    [Fact]
    public async Task OwnerScopeAndTerminalStatusAreEnforced()
    {
        var fixture = new ServiceFixture();
        await fixture.Recommendations.ProcessAnalyticsAsync(
            AnalyticsEnvelope("analytics-owner", "owner-a", 1_000m, 900m),
            CancellationToken.None);
        var own = await fixture.Notifications.GetAsync(
            "owner-a",
            "USD",
            CancellationToken.None);
        var notification = Assert.Single(
            own,
            item => item.Channel == NotificationChannels.Push);

        var otherOwner = await fixture.Notifications.UpdateStatusAsync(
            "owner-b",
            notification.NotificationId,
            NotificationDeliveryStatuses.Delivered,
            Now.AddMinutes(1),
            CancellationToken.None);
        var delivered = await fixture.Notifications.UpdateStatusAsync(
            "owner-a",
            notification.NotificationId,
            NotificationDeliveryStatuses.Delivered,
            Now.AddMinutes(1),
            CancellationToken.None);

        Assert.Null(otherOwner);
        Assert.Equal(NotificationDeliveryStatuses.Delivered, delivered!.DeliveryStatus);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Notifications.UpdateStatusAsync(
                "owner-a",
                notification.NotificationId,
                NotificationDeliveryStatuses.Failed,
                Now.AddMinutes(2),
                CancellationToken.None));
    }

    [Fact]
    public async Task Dismissal_IsOwnerScopedAndTerminal()
    {
        var fixture = new ServiceFixture();
        var generated = await fixture.Recommendations.ProcessAnalyticsAsync(
            AnalyticsEnvelope("analytics-dismiss", "owner-a", 1_000m, 900m),
            CancellationToken.None);
        var recommendation = generated[0];

        var otherOwner = await fixture.Recommendations.MarkReadAsync(
            "owner-b",
            recommendation.RecommendationId,
            Now.AddMinutes(1),
            CancellationToken.None);
        var read = await fixture.Recommendations.MarkReadAsync(
            "owner-a",
            recommendation.RecommendationId,
            Now.AddMinutes(1),
            CancellationToken.None);
        var dismissed = await fixture.Recommendations.DismissAsync(
            "owner-a",
            recommendation.RecommendationId,
            Now.AddMinutes(2),
            CancellationToken.None);

        Assert.Null(otherOwner);
        Assert.Equal(RecommendationStatuses.Read, read!.Status);
        Assert.Equal(Now.AddMinutes(1), read.StatusChangedAtUtc);
        Assert.Equal(RecommendationStatuses.Dismissed, dismissed!.Status);
        Assert.Equal(Now.AddMinutes(2), dismissed.StatusChangedAtUtc);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Store.UpdateRecommendationStatusAsync(
                "owner-a",
                recommendation.RecommendationId,
                RecommendationStatuses.Active,
                Now.AddMinutes(2),
                CancellationToken.None));
    }

    [Fact]
    public async Task NewerFacts_ExpireSupersededActiveRecommendations()
    {
        var fixture = new ServiceFixture();
        var first = await fixture.Recommendations.ProcessAnalyticsAsync(
            AnalyticsEnvelope("analytics-first", "owner-a", 1_000m, 900m),
            CancellationToken.None);
        var prior = first[0];
        var replacementTime = Now.AddMinutes(2);

        var current = await fixture.Recommendations.ProcessAnalyticsAsync(
            AnalyticsEnvelope(
                "analytics-replacement",
                "owner-a",
                1_000m,
                100m,
                replacementTime),
            CancellationToken.None);
        var stored = await fixture.Recommendations.GetAsync(
            "owner-a",
            "USD",
            CancellationToken.None);

        var expired = Assert.Single(
            stored,
            item => item.RecommendationId == prior.RecommendationId);
        Assert.Equal(RecommendationStatuses.Expired, expired.Status);
        Assert.Equal(replacementTime, expired.StatusChangedAtUtc);
        Assert.All(
            current,
            item => Assert.Equal(RecommendationStatuses.Active, item.Status));
    }

    [Fact]
    public async Task ProfileSettings_DriveBudgetAndCompletenessRules()
    {
        var fixture = new ServiceFixture();
        fixture.ProfileSettings.Set(
            "owner-a",
            "USD",
            new RecommendationProfileSettings(true, false, 1_000m));

        var recommendations = await fixture.Recommendations.ProcessAnalyticsAsync(
            AnalyticsEnvelope(
                "analytics-profile-rules",
                "owner-a",
                1_000m,
                850m,
                topExpenseCategoryAmount: 400m,
                uncategorizedExpenseTotal: 50m),
            CancellationToken.None);

        Assert.Contains(recommendations, item => item.Code == "monthly-budget-nearing-limit");
        Assert.Contains(recommendations, item => item.Code == "incomplete-profile");
        Assert.Contains(recommendations, item => item.Code == "high-spending-category");
        Assert.Contains(recommendations, item => item.Code == "uncategorized-expenses");
    }

    [Fact]
    public async Task ScoreEvent_UsesBackendScoreWithoutChangingIt()
    {
        var fixture = new ServiceFixture();
        var envelope = new IntegrationEventEnvelope<ScoreCalculatedV1>(
            "score-1",
            "score-occurrence-1",
            FinancialScoreEventTypes.ScoreCalculated,
            Now,
            "financial-assistant-financial-score-service",
            FinancialScoreEventTypes.SchemaVersion,
            "correlation-1",
            "financial-event-1",
            "owner-a",
            new ScoreCalculatedV1(
                "calculation-1",
                "USD",
                38,
                "financial-score-v1",
                Array.Empty<FinancialScoreFactorV1>(),
                Now));

        var recommendations = await fixture.Recommendations.ProcessScoreAsync(
            envelope,
            CancellationToken.None);

        var recommendation = Assert.Single(recommendations);
        Assert.Equal("score-recovery", recommendation.Code);
        Assert.Contains(
            recommendation.Facts,
            item => item.Code == "score" && item.Value == 38m);
    }

    private static IntegrationEventEnvelope<AnalyticsUpdatedV1> AnalyticsEnvelope(
        string eventId,
        string owner,
        decimal income,
        decimal expense,
        DateTimeOffset? updatedAt = null,
        decimal topExpenseCategoryAmount = 0m,
        decimal uncategorizedExpenseTotal = 0m) =>
        new(
            eventId,
            $"{eventId}-occurrence",
            AnalyticsEventTypes.AnalyticsUpdated,
            updatedAt ?? Now,
            "financial-assistant-analytics-service",
            AnalyticsEventTypes.SchemaVersion,
            "correlation-1",
            "financial-event-1",
            owner,
            new AnalyticsUpdatedV1(
                "USD",
                new DateOnly(2026, 8, 2),
                income,
                expense,
                100m,
                20m,
                "food",
                updatedAt ?? Now,
                topExpenseCategoryAmount,
                uncategorizedExpenseTotal));

    private sealed class ServiceFixture
    {
        public ServiceFixture()
        {
            Store = new InMemoryRecommendationNotificationStore();
            NotificationPublisher = new InMemoryNotificationEventPublisher();
            NotificationPreferences = new InMemoryNotificationPreferenceProvider();
            var templates = new NotificationTemplateCatalog();
            Notifications = new NotificationPreparationService(
                Store,
                templates,
                NotificationPublisher,
                NotificationPreferences);
            Triggers = new NotificationTriggerService(
                Store,
                new NotificationTriggerEvaluator(),
                templates,
                NotificationPublisher,
                NotificationPreferences);
            RecommendationPublisher = new InMemoryRecommendationEventPublisher(Notifications);
            ProfileSettings = new InMemoryRecommendationProfileSettingsProvider();
            Recommendations = new RecommendationService(
                Store,
                new RecommendationGenerator(),
                new DeterministicRecommendationWordingProvider(),
                RecommendationPublisher,
                ProfileSettings);
        }

        public InMemoryRecommendationNotificationStore Store { get; }

        public InMemoryNotificationEventPublisher NotificationPublisher { get; }

        public InMemoryNotificationPreferenceProvider NotificationPreferences { get; }

        public InMemoryRecommendationEventPublisher RecommendationPublisher { get; }

        public InMemoryRecommendationProfileSettingsProvider ProfileSettings { get; }

        public RecommendationService Recommendations { get; }

        public NotificationPreparationService Notifications { get; }

        public NotificationTriggerService Triggers { get; }
    }

    private sealed class FailOnceNotificationPublisher : INotificationEventPublisher
    {
        private bool shouldFail = true;
        private readonly List<IntegrationEventEnvelope<NotificationPreparedV1>> published = [];

        public IReadOnlyList<IntegrationEventEnvelope<NotificationPreparedV1>> Published => published;

        public Task PublishAsync(
            IntegrationEventEnvelope<NotificationPreparedV1> envelope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (shouldFail)
            {
                shouldFail = false;
                throw new InvalidOperationException("Synthetic transient publication failure.");
            }

            published.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
