namespace FinancialAssistant.RecommendationsNotifications.Domain;

public static class RecommendationSeverities
{
    public const string Information = "information";
    public const string Warning = "warning";
    public const string Critical = "critical";
}

public static class RecommendationStatuses
{
    public const string Active = "active";
    public const string Read = "read";
    public const string Dismissed = "dismissed";
    public const string Expired = "expired";

    public static bool IsTerminal(string value) =>
        value is Dismissed or Expired;

    public static bool CanTransition(string current, string next) =>
        current == next ||
        (current == Active && next == Read) ||
        (!IsTerminal(current) && IsTerminal(next));
}

public static class NotificationChannels
{
    public const string Push = "push";
    public const string Web = "web";
}

public sealed record NotificationQuietHours(
    TimeOnly StartsAt,
    TimeOnly EndsAt,
    string TimeZoneId);

public sealed record NotificationPreferences(
    bool PushEnabled,
    bool WebEnabled,
    NotificationQuietHours? QuietHours = null,
    IReadOnlyList<string>? DisabledNotificationTypes = null)
{
    public static NotificationPreferences AllEnabled { get; } =
        new(true, true, null, Array.Empty<string>());

    public bool IsEnabled(string channel) =>
        channel switch
        {
            NotificationChannels.Push => PushEnabled,
            NotificationChannels.Web => WebEnabled,
            _ => false
        };

    public bool IsEnabled(string channel, string notificationType) =>
        IsEnabled(channel) &&
        DisabledNotificationTypes?.Contains(
            notificationType,
            StringComparer.Ordinal) != true;
}

public static class NotificationDeliveryStatuses
{
    public const string Prepared = "prepared";
    public const string RetryScheduled = "retry-scheduled";
    public const string Delivered = "delivered";
    public const string Failed = "failed";
    public const string Suppressed = "suppressed";

    public static bool IsKnown(string value) =>
        value is Prepared or RetryScheduled or Delivered or Failed or Suppressed;

    public static bool IsTerminal(string value) =>
        value is Delivered or Failed or Suppressed;
}

public sealed record AnalyticsInsightFacts(
    DateOnly ReferenceDate,
    decimal MonthlyIncomeTotal,
    decimal MonthlyExpenseTotal,
    decimal? DailyExpenseLimit,
    decimal DailyExpenseSpent,
    string? TopExpenseCategoryId,
    DateTimeOffset UpdatedAtUtc,
    decimal TopExpenseCategoryAmount = 0m,
    decimal UncategorizedExpenseTotal = 0m);

public sealed record RecommendationProfileSettings(
    bool IsAvailable,
    bool IsComplete,
    decimal? MonthlyBudgetLimit)
{
    public static RecommendationProfileSettings Unavailable { get; } =
        new(false, false, null);
}

public sealed record ScoreInsightFacts(
    int Score,
    string FormulaVersion,
    DateTimeOffset CalculatedAtUtc);

public sealed record InsightSnapshot(
    string UserIdHash,
    string Currency,
    AnalyticsInsightFacts? Analytics,
    ScoreInsightFacts? Score,
    RecommendationProfileSettings? Profile = null);

public sealed record RecommendationFact(
    string Code,
    decimal Value);

public sealed record FinancialRecommendation(
    string RecommendationId,
    string UserIdHash,
    string Currency,
    string Code,
    string Severity,
    string Title,
    string Body,
    IReadOnlyList<RecommendationFact> Facts,
    string SourceEventId,
    DateTimeOffset GeneratedAtUtc,
    string Status,
    DateTimeOffset StatusChangedAtUtc,
    RecommendationExplanation? Explanation = null);

public sealed record PreparedNotification(
    string NotificationId,
    string RecommendationId,
    string UserIdHash,
    string Currency,
    string Channel,
    string TemplateCode,
    string Title,
    string Body,
    string DeliveryStatus,
    string SourceEventId,
    DateTimeOffset PreparedAtUtc,
    DateTimeOffset? StatusChangedAtUtc);
