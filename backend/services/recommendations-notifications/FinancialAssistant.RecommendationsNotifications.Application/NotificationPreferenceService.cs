using FinancialAssistant.RecommendationsNotifications.Domain;

namespace FinancialAssistant.RecommendationsNotifications.Application;

public sealed class NotificationPreferenceService(
    INotificationPreferenceProvider provider)
{
    public Task<NotificationPreferences> GetAsync(
        string userIdHash,
        CancellationToken cancellationToken) =>
        provider.GetAsync(
            NormalizeOwner(userIdHash),
            cancellationToken);

    public Task<NotificationPreferences> UpdateAsync(
        string userIdHash,
        bool pushEnabled,
        bool webEnabled,
        IReadOnlyList<string> enabledNotificationTypes,
        NotificationQuietHours? quietHours,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(enabledNotificationTypes);
        var enabled = enabledNotificationTypes
            .Select(NormalizeType)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var unknown = enabled
            .Where(type => !NotificationTriggerCodes.IsKnown(type))
            .OrderBy(type => type, StringComparer.Ordinal)
            .ToArray();
        if (unknown.Length > 0)
        {
            throw new ArgumentException(
                $"Unknown notification type: {string.Join(", ", unknown)}.",
                nameof(enabledNotificationTypes));
        }

        var normalizedQuietHours = NormalizeQuietHours(quietHours);
        var disabled = NotificationTriggerCodes.All
            .Except(enabled, StringComparer.Ordinal)
            .ToArray();
        return provider.UpdateAsync(
            NormalizeOwner(userIdHash),
            new NotificationPreferences(
                pushEnabled,
                webEnabled,
                normalizedQuietHours,
                disabled),
            cancellationToken);
    }

    private static NotificationQuietHours? NormalizeQuietHours(
        NotificationQuietHours? quietHours)
    {
        if (quietHours is null)
        {
            return null;
        }

        if (quietHours.StartsAt == quietHours.EndsAt)
        {
            throw new ArgumentException(
                "Quiet hours start and end must be different.",
                nameof(quietHours));
        }

        if (string.IsNullOrWhiteSpace(quietHours.TimeZoneId))
        {
            throw new ArgumentException(
                "Quiet hours time zone is required.",
                nameof(quietHours));
        }

        return quietHours with { TimeZoneId = quietHours.TimeZoneId.Trim() };
    }

    private static string NormalizeType(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(
                "Notification type values cannot be empty.",
                nameof(value))
            : value.Trim().ToLowerInvariant();

    private static string NormalizeOwner(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Owner hash is required.", nameof(value))
            : value.Trim();
}
