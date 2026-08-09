using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public sealed class InMemoryRecommendationNotificationStore : IRecommendationNotificationStore
{
    private readonly object gate = new();
    private readonly HashSet<string> processedEvents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, InsightSnapshot> snapshots = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (DateTimeOffset Timestamp, string EventId)> latestAnalytics =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, (DateTimeOffset Timestamp, string EventId)> latestScores =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, FinancialRecommendation[]> recommendations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PreparedNotification> notifications = new(StringComparer.Ordinal);
    private readonly HashSet<string> publishedNotifications = new(StringComparer.Ordinal);

    public Task<InsightApplyResult> ApplyAnalyticsIfNewAsync(
        string sourceEventId,
        string userIdHash,
        string currency,
        AnalyticsInsightFacts analytics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var key = Scope(userIdHash, currency);
            var existing = snapshots.GetValueOrDefault(key) ??
                new InsightSnapshot(userIdHash, currency, null, null);
            var processedKey = Processed(userIdHash, sourceEventId);
            if (processedEvents.Contains(processedKey))
            {
                return Task.FromResult(new InsightApplyResult(false, existing));
            }

            if (IsSame(latestAnalytics.GetValueOrDefault(key), analytics.UpdatedAtUtc, sourceEventId))
            {
                return Task.FromResult(new InsightApplyResult(true, existing));
            }

            if (!IsNewer(latestAnalytics.GetValueOrDefault(key), analytics.UpdatedAtUtc, sourceEventId))
            {
                processedEvents.Add(processedKey);
                return Task.FromResult(new InsightApplyResult(false, existing));
            }

            var updated = existing with { Analytics = analytics };
            snapshots[key] = updated;
            latestAnalytics[key] = (analytics.UpdatedAtUtc, sourceEventId);
            return Task.FromResult(new InsightApplyResult(true, updated));
        }
    }

    public Task<InsightApplyResult> ApplyScoreIfNewAsync(
        string sourceEventId,
        string userIdHash,
        string currency,
        ScoreInsightFacts score,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var key = Scope(userIdHash, currency);
            var existing = snapshots.GetValueOrDefault(key) ??
                new InsightSnapshot(userIdHash, currency, null, null);
            var processedKey = Processed(userIdHash, sourceEventId);
            if (processedEvents.Contains(processedKey))
            {
                return Task.FromResult(new InsightApplyResult(false, existing));
            }

            if (IsSame(latestScores.GetValueOrDefault(key), score.CalculatedAtUtc, sourceEventId))
            {
                return Task.FromResult(new InsightApplyResult(true, existing));
            }

            if (!IsNewer(latestScores.GetValueOrDefault(key), score.CalculatedAtUtc, sourceEventId))
            {
                processedEvents.Add(processedKey);
                return Task.FromResult(new InsightApplyResult(false, existing));
            }

            var updated = existing with { Score = score };
            snapshots[key] = updated;
            latestScores[key] = (score.CalculatedAtUtc, sourceEventId);
            return Task.FromResult(new InsightApplyResult(true, updated));
        }
    }

    public Task MarkInsightEventCompletedAsync(
        string sourceEventId,
        string userIdHash,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            processedEvents.Add(Processed(userIdHash, sourceEventId));
        }

        return Task.CompletedTask;
    }

    public Task ReplaceCurrentRecommendationsAsync(
        string userIdHash,
        string currency,
        IReadOnlyList<FinancialRecommendation> values,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var key = Scope(userIdHash, currency);
            var incomingIds = values
                .Select(item => item.RecommendationId)
                .ToHashSet(StringComparer.Ordinal);
            var lifecycleTimestamp = changedAtUtc.ToUniversalTime();
            var previous = recommendations.GetValueOrDefault(key) ??
                Array.Empty<FinancialRecommendation>();
            var superseded = previous
                .Where(item => !incomingIds.Contains(item.RecommendationId))
                .Select(item =>
                    !RecommendationStatuses.IsTerminal(item.Status)
                        ? item with
                        {
                            Status = RecommendationStatuses.Expired,
                            StatusChangedAtUtc = lifecycleTimestamp > item.StatusChangedAtUtc
                                ? lifecycleTimestamp
                                : item.StatusChangedAtUtc
                        }
                        : item);

            recommendations[key] = superseded
                .Concat(values)
                .OrderBy(item => RecommendationStatuses.IsTerminal(item.Status))
                .ThenByDescending(item => SeverityRank(item.Severity))
                .ThenByDescending(item => item.GeneratedAtUtc)
                .ThenBy(item => item.Code, StringComparer.Ordinal)
                .ToArray();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FinancialRecommendation>> GetRecommendationsAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<FinancialRecommendation>>(
                recommendations.GetValueOrDefault(Scope(userIdHash, currency)) ??
                Array.Empty<FinancialRecommendation>());
        }
    }

    public Task<FinancialRecommendation?> UpdateRecommendationStatusAsync(
        string userIdHash,
        string recommendationId,
        string status,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            foreach (var (key, values) in recommendations)
            {
                var index = Array.FindIndex(
                    values,
                    item =>
                        item.RecommendationId == recommendationId &&
                        item.UserIdHash == userIdHash);
                if (index < 0)
                {
                    continue;
                }

                var existing = values[index];
                if (existing.Status == status)
                {
                    return Task.FromResult<FinancialRecommendation?>(existing);
                }

                if (!RecommendationStatuses.CanTransition(existing.Status, status))
                {
                    throw new InvalidOperationException(
                        "A terminal recommendation status cannot be changed.");
                }

                if (changedAtUtc < existing.StatusChangedAtUtc)
                {
                    throw new InvalidOperationException(
                        "Recommendation status time cannot precede the current lifecycle state.");
                }

                var updated = existing with
                {
                    Status = status,
                    StatusChangedAtUtc = changedAtUtc
                };
                var replacement = values.ToArray();
                replacement[index] = updated;
                recommendations[key] = replacement;
                return Task.FromResult<FinancialRecommendation?>(updated);
            }

            return Task.FromResult<FinancialRecommendation?>(null);
        }
    }

    public Task<IReadOnlyList<PreparedNotification>> SaveNotificationsIfNewAsync(
        IReadOnlyList<PreparedNotification> values,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var accepted = new List<PreparedNotification>();
        lock (gate)
        {
            foreach (var notification in values)
            {
                if (notifications.TryGetValue(notification.NotificationId, out var existing))
                {
                    if (!publishedNotifications.Contains(notification.NotificationId))
                    {
                        accepted.Add(existing);
                    }

                    continue;
                }

                notifications.Add(notification.NotificationId, notification);
                accepted.Add(notification);
            }
        }

        return Task.FromResult<IReadOnlyList<PreparedNotification>>(accepted);
    }

    public Task MarkNotificationPublishedAsync(
        string notificationId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!notifications.ContainsKey(notificationId))
            {
                throw new InvalidOperationException("Notification must be saved before publication.");
            }

            publishedNotifications.Add(notificationId);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PreparedNotification>> GetNotificationsAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<PreparedNotification>>(
                notifications.Values
                    .Where(item =>
                        item.UserIdHash == userIdHash &&
                        item.Currency == currency)
                    .OrderByDescending(item => item.PreparedAtUtc)
                    .ThenBy(item => item.NotificationId, StringComparer.Ordinal)
                    .ToArray());
        }
    }

    public Task<PreparedNotification?> UpdateNotificationStatusAsync(
        string userIdHash,
        string notificationId,
        string deliveryStatus,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!notifications.TryGetValue(notificationId, out var existing) ||
                existing.UserIdHash != userIdHash)
            {
                return Task.FromResult<PreparedNotification?>(null);
            }

            if (existing.DeliveryStatus == deliveryStatus)
            {
                return Task.FromResult<PreparedNotification?>(existing);
            }

            if (existing.DeliveryStatus != NotificationDeliveryStatuses.Prepared)
            {
                throw new InvalidOperationException(
                    "A terminal notification delivery status cannot be changed.");
            }

            var updated = existing with
            {
                DeliveryStatus = deliveryStatus,
                StatusChangedAtUtc = changedAtUtc
            };
            notifications[notificationId] = updated;
            return Task.FromResult<PreparedNotification?>(updated);
        }
    }

    private static string Scope(string userIdHash, string currency) =>
        $"{userIdHash}|{currency}";

    private static string Processed(string userIdHash, string sourceEventId) =>
        $"{userIdHash}|{sourceEventId}";

    private static int SeverityRank(string severity) =>
        severity switch
        {
            RecommendationSeverities.Critical => 3,
            RecommendationSeverities.Warning => 2,
            _ => 1
        };

    private static bool IsNewer(
        (DateTimeOffset Timestamp, string EventId) current,
        DateTimeOffset candidateTimestamp,
        string candidateEventId) =>
        current == default ||
        candidateTimestamp > current.Timestamp ||
        (candidateTimestamp == current.Timestamp &&
         string.CompareOrdinal(candidateEventId, current.EventId) > 0);

    private static bool IsSame(
        (DateTimeOffset Timestamp, string EventId) current,
        DateTimeOffset candidateTimestamp,
        string candidateEventId) =>
        current != default &&
        candidateTimestamp == current.Timestamp &&
        candidateEventId == current.EventId;
}
