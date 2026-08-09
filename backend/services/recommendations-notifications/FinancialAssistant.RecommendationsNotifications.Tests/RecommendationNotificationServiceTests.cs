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
            Notifications = new NotificationPreparationService(
                Store,
                new NotificationTemplateCatalog(),
                NotificationPublisher);
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

        public InMemoryRecommendationEventPublisher RecommendationPublisher { get; }

        public InMemoryRecommendationProfileSettingsProvider ProfileSettings { get; }

        public RecommendationService Recommendations { get; }

        public NotificationPreparationService Notifications { get; }
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
