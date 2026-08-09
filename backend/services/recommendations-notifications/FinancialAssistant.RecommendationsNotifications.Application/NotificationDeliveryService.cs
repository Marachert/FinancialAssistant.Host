using FinancialAssistant.RecommendationsNotifications.Domain;

namespace FinancialAssistant.RecommendationsNotifications.Application;

public interface INotificationDeliveryAdapter
{
    string Channel { get; }

    Task<NotificationDeliveryAdapterResult> SendAsync(
        PreparedNotification notification,
        CancellationToken cancellationToken);
}

public interface INotificationRetryPolicy
{
    NotificationRetryDecision Decide(
        int currentAttemptNumber,
        bool isTransientFailure,
        DateTimeOffset attemptedAtUtc);
}

public sealed class NotificationDeliveryService
{
    private readonly IReadOnlyDictionary<string, INotificationDeliveryAdapter> adapters;
    private readonly INotificationRetryPolicy retryPolicy;

    public NotificationDeliveryService(
        IEnumerable<INotificationDeliveryAdapter> adapters,
        INotificationRetryPolicy retryPolicy)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        this.retryPolicy = retryPolicy ??
            throw new ArgumentNullException(nameof(retryPolicy));

        var byChannel = new Dictionary<string, INotificationDeliveryAdapter>(
            StringComparer.Ordinal);
        foreach (var adapter in adapters)
        {
            ArgumentNullException.ThrowIfNull(adapter);
            if (!byChannel.TryAdd(adapter.Channel, adapter))
            {
                throw new InvalidOperationException(
                    $"Only one delivery adapter may own channel '{adapter.Channel}'.");
            }
        }

        this.adapters = byChannel;
    }

    public async Task<NotificationDeliveryAttempt> DeliverAsync(
        PreparedNotification notification,
        int attemptNumber,
        DateTimeOffset attemptedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        if (notification.DeliveryStatus != NotificationDeliveryStatuses.Prepared)
        {
            throw new ArgumentException(
                "Only prepared notifications can enter delivery.",
                nameof(notification));
        }

        if (attemptNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptNumber));
        }

        if (attemptedAtUtc == default)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptedAtUtc));
        }

        if (!adapters.TryGetValue(notification.Channel, out var adapter))
        {
            throw new InvalidOperationException(
                $"No delivery adapter is registered for channel '{notification.Channel}'.");
        }

        var result = await adapter.SendAsync(notification, cancellationToken);
        ValidateResult(result);

        var retry = result.Status == NotificationDeliveryStatuses.Failed
            ? retryPolicy.Decide(
                attemptNumber,
                result.IsTransientFailure,
                attemptedAtUtc)
            : NotificationRetryDecision.NoRetry(attemptNumber);
        var status = retry.ShouldRetry
            ? NotificationDeliveryStatuses.RetryScheduled
            : result.Status;

        return new NotificationDeliveryAttempt(
            notification.NotificationId,
            notification.Channel,
            attemptNumber,
            status,
            retry.ShouldRetry,
            result.FailureCode,
            attemptedAtUtc.ToUniversalTime(),
            retry.RetryAtUtc?.ToUniversalTime());
    }

    private static void ValidateResult(NotificationDeliveryAdapterResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Status is not (
                NotificationDeliveryStatuses.Delivered or
                NotificationDeliveryStatuses.Failed or
                NotificationDeliveryStatuses.Suppressed))
        {
            throw new InvalidOperationException(
                "Delivery adapters must return delivered, failed, or suppressed.");
        }

        if (result.Status == NotificationDeliveryStatuses.Delivered &&
            (result.IsTransientFailure || result.FailureCode is not null))
        {
            throw new InvalidOperationException(
                "Delivered results cannot contain failure metadata.");
        }

        if (result.Status != NotificationDeliveryStatuses.Delivered &&
            string.IsNullOrWhiteSpace(result.FailureCode))
        {
            throw new InvalidOperationException(
                "Failed and suppressed results require a privacy-safe failure code.");
        }

        if (result.Status == NotificationDeliveryStatuses.Suppressed &&
            result.IsTransientFailure)
        {
            throw new InvalidOperationException(
                "Suppressed results cannot be transient failures.");
        }
    }
}
