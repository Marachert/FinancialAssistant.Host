using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.RecommendationsNotifications.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinancialAssistant.RecommendationsNotifications.Tests;

public sealed class NotificationDeliveryAdapterTests
{
    private static readonly DateTimeOffset AttemptedAt =
        new(2026, 8, 9, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task DeliveryService_RoutesPreparedNotificationByChannel()
    {
        var push = new StubAdapter(
            NotificationChannels.Push,
            NotificationDeliveryAdapterResult.Delivered());
        var web = new StubAdapter(
            NotificationChannels.Web,
            NotificationDeliveryAdapterResult.Delivered());
        var service = new NotificationDeliveryService(
            new INotificationDeliveryAdapter[] { push, web },
            new StubRetryPolicy(NotificationRetryDecision.NoRetry(1)));

        var attempt = await service.DeliverAsync(
            CreatePrepared(NotificationChannels.Push),
            1,
            AttemptedAt,
            CancellationToken.None);

        Assert.Equal(NotificationDeliveryStatuses.Delivered, attempt.Status);
        Assert.False(attempt.IsRetryable);
        Assert.Null(attempt.FailureCode);
        Assert.Null(attempt.RetryAtUtc);
        Assert.Single(push.Sent);
        Assert.Empty(web.Sent);
    }

    [Fact]
    public async Task TransientFailure_UsesRetryPolicyWithoutChangingMessage()
    {
        var retryAt = AttemptedAt.AddSeconds(30);
        var adapter = new StubAdapter(
            NotificationChannels.Web,
            NotificationDeliveryAdapterResult.Failed(
                NotificationDeliveryFailureCodes.ProviderUnavailable,
                true));
        var service = new NotificationDeliveryService(
            new[] { adapter },
            new StubRetryPolicy(new NotificationRetryDecision(true, 2, retryAt)));
        var notification = CreatePrepared(NotificationChannels.Web);

        var attempt = await service.DeliverAsync(
            notification,
            1,
            AttemptedAt,
            CancellationToken.None);

        Assert.Equal(NotificationDeliveryStatuses.RetryScheduled, attempt.Status);
        Assert.True(attempt.IsRetryable);
        Assert.Equal(
            NotificationDeliveryFailureCodes.ProviderUnavailable,
            attempt.FailureCode);
        Assert.Equal(retryAt, attempt.RetryAtUtc);
        Assert.Same(notification, Assert.Single(adapter.Sent));
    }

    [Fact]
    public async Task PlaceholderAdapters_UseConfigurationAndNeverClaimDelivery()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RecommendationsNotifications:Delivery:Push:Enabled"] = "false",
                ["RecommendationsNotifications:Delivery:Web:Enabled"] = "true",
                ["RecommendationsNotifications:Delivery:Web:Provider"] =
                    "synthetic-provider",
                ["RecommendationsNotifications:Delivery:Web:Credential"] =
                    "synthetic-credential",
                ["RecommendationsNotifications:Delivery:Retry:MaxAttempts"] = "2",
                ["RecommendationsNotifications:Delivery:Retry:DelaySeconds"] = "15"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddRecommendationNotificationInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        var adapters = provider
            .GetServices<INotificationDeliveryAdapter>()
            .ToDictionary(item => item.Channel, StringComparer.Ordinal);

        var push = await adapters[NotificationChannels.Push].SendAsync(
            CreatePrepared(NotificationChannels.Push),
            CancellationToken.None);
        var web = await adapters[NotificationChannels.Web].SendAsync(
            CreatePrepared(NotificationChannels.Web),
            CancellationToken.None);
        var retry = provider
            .GetRequiredService<INotificationRetryPolicy>()
            .Decide(1, true, AttemptedAt);

        Assert.Equal(NotificationDeliveryStatuses.Suppressed, push.Status);
        Assert.Equal(
            NotificationDeliveryFailureCodes.ChannelDisabled,
            push.FailureCode);
        Assert.Equal(NotificationDeliveryStatuses.Failed, web.Status);
        Assert.False(web.IsTransientFailure);
        Assert.Equal(
            NotificationDeliveryFailureCodes.ProviderAdapterPlaceholder,
            web.FailureCode);
        Assert.True(retry.ShouldRetry);
        Assert.Equal(2, retry.NextAttemptNumber);
        Assert.Equal(AttemptedAt.AddSeconds(15), retry.RetryAtUtc);
    }

    [Fact]
    public async Task EnabledPlaceholderWithoutCompleteConfiguration_FailsPermanently()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RecommendationsNotifications:Delivery:Push:Enabled"] = "true",
                ["RecommendationsNotifications:Delivery:Push:Provider"] =
                    "synthetic-provider"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddRecommendationNotificationInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        var adapter = provider
            .GetServices<INotificationDeliveryAdapter>()
            .Single(item => item.Channel == NotificationChannels.Push);

        var result = await adapter.SendAsync(
            CreatePrepared(NotificationChannels.Push),
            CancellationToken.None);

        Assert.Equal(NotificationDeliveryStatuses.Failed, result.Status);
        Assert.False(result.IsTransientFailure);
        Assert.Equal(
            NotificationDeliveryFailureCodes.ProviderNotConfigured,
            result.FailureCode);
    }

    [Fact]
    public void DeliveryStatuses_AreExplicitAndRetryScheduledIsNonterminal()
    {
        Assert.True(NotificationDeliveryStatuses.IsKnown(
            NotificationDeliveryStatuses.Prepared));
        Assert.True(NotificationDeliveryStatuses.IsKnown(
            NotificationDeliveryStatuses.RetryScheduled));
        Assert.True(NotificationDeliveryStatuses.IsKnown(
            NotificationDeliveryStatuses.Delivered));
        Assert.True(NotificationDeliveryStatuses.IsKnown(
            NotificationDeliveryStatuses.Failed));
        Assert.True(NotificationDeliveryStatuses.IsKnown(
            NotificationDeliveryStatuses.Suppressed));
        Assert.False(NotificationDeliveryStatuses.IsTerminal(
            NotificationDeliveryStatuses.Prepared));
        Assert.False(NotificationDeliveryStatuses.IsTerminal(
            NotificationDeliveryStatuses.RetryScheduled));
        Assert.True(NotificationDeliveryStatuses.IsTerminal(
            NotificationDeliveryStatuses.Delivered));
    }

    private static PreparedNotification CreatePrepared(string channel) =>
        new(
            $"notification-{channel}",
            "recommendation-1",
            new string('a', 64),
            "USD",
            channel,
            "recommendation-available.v1",
            "A new insight is ready",
            "Open Financial Assistant to review it.",
            NotificationDeliveryStatuses.Prepared,
            "synthetic-source-event",
            AttemptedAt.AddMinutes(-1),
            null);

    private sealed class StubAdapter(
        string channel,
        NotificationDeliveryAdapterResult result)
        : INotificationDeliveryAdapter
    {
        private readonly List<PreparedNotification> sent = [];

        public string Channel { get; } = channel;

        public IReadOnlyList<PreparedNotification> Sent => sent;

        public Task<NotificationDeliveryAdapterResult> SendAsync(
            PreparedNotification notification,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sent.Add(notification);
            return Task.FromResult(result);
        }
    }

    private sealed class StubRetryPolicy(NotificationRetryDecision decision)
        : INotificationRetryPolicy
    {
        public NotificationRetryDecision Decide(
            int currentAttemptNumber,
            bool isTransientFailure,
            DateTimeOffset attemptedAtUtc) =>
            decision;
    }
}
