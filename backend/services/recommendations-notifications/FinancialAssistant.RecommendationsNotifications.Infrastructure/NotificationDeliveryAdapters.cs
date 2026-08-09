using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public sealed class MobilePushNotificationDeliveryAdapter(
    IOptions<RecommendationNotificationServiceOptions> options)
    : PlaceholderNotificationDeliveryAdapter(
        NotificationChannels.Push,
        options.Value.Delivery.Push);

public sealed class WebNotificationDeliveryAdapter(
    IOptions<RecommendationNotificationServiceOptions> options)
    : PlaceholderNotificationDeliveryAdapter(
        NotificationChannels.Web,
        options.Value.Delivery.Web);

public abstract class PlaceholderNotificationDeliveryAdapter(
    string channel,
    NotificationDeliveryProviderOptions options)
    : INotificationDeliveryAdapter
{
    public string Channel { get; } = channel;

    public Task<NotificationDeliveryAdapterResult> SendAsync(
        PreparedNotification notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        cancellationToken.ThrowIfCancellationRequested();
        if (notification.Channel != Channel)
        {
            throw new ArgumentException(
                $"The {Channel} adapter cannot send channel '{notification.Channel}'.",
                nameof(notification));
        }

        if (!options.Enabled)
        {
            return Task.FromResult(
                NotificationDeliveryAdapterResult.Suppressed(
                    NotificationDeliveryFailureCodes.ChannelDisabled));
        }

        if (string.IsNullOrWhiteSpace(options.Provider) ||
            string.IsNullOrWhiteSpace(options.Credential))
        {
            return Task.FromResult(
                NotificationDeliveryAdapterResult.Failed(
                    NotificationDeliveryFailureCodes.ProviderNotConfigured,
                    false));
        }

        return Task.FromResult(
            NotificationDeliveryAdapterResult.Failed(
                NotificationDeliveryFailureCodes.ProviderAdapterPlaceholder,
                false));
    }
}

public sealed class ConfiguredNotificationRetryPolicy
    : INotificationRetryPolicy
{
    private readonly NotificationDeliveryRetryOptions options;

    public ConfiguredNotificationRetryPolicy(
        IOptions<RecommendationNotificationServiceOptions> options)
    {
        this.options = options.Value.Delivery.Retry;
        if (this.options.MaxAttempts is < 1 or > 10)
        {
            throw new InvalidOperationException(
                "Notification delivery MaxAttempts must be between 1 and 10.");
        }

        if (this.options.DelaySeconds is < 1 or > 3600)
        {
            throw new InvalidOperationException(
                "Notification delivery DelaySeconds must be between 1 and 3600.");
        }
    }

    public NotificationRetryDecision Decide(
        int currentAttemptNumber,
        bool isTransientFailure,
        DateTimeOffset attemptedAtUtc)
    {
        if (currentAttemptNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentAttemptNumber));
        }

        if (attemptedAtUtc == default)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptedAtUtc));
        }

        if (!isTransientFailure || currentAttemptNumber >= options.MaxAttempts)
        {
            return NotificationRetryDecision.NoRetry(currentAttemptNumber);
        }

        return new NotificationRetryDecision(
            true,
            currentAttemptNumber + 1,
            attemptedAtUtc.ToUniversalTime().AddSeconds(options.DelaySeconds));
    }
}
