namespace FinancialAssistant.Shared.Contracts.Events;

public static class NotificationEventTypes
{
    public const int SchemaVersion = 1;
    public const string NotificationPrepared = "notification.prepared.v1";
}

public sealed record NotificationPreparedV1(
    string NotificationId,
    string RecommendationId,
    string Currency,
    string Channel,
    string TemplateCode,
    string DeliveryStatus,
    DateTimeOffset PreparedAtUtc);
