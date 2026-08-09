namespace FinancialAssistant.RecommendationsNotifications.Contracts;

public static class RecommendationNotificationApiRoutes
{
    public const string Recommendations = "/api/v1/recommendations";
    public const string GatewayRecommendations = "/recommendations";
    public const string RecommendationDismissal =
        "/api/v1/recommendations/{recommendationId}/dismissal";
    public const string GatewayRecommendationDismissal =
        "/recommendations/{recommendationId}/dismissal";
    public const string RecommendationRead =
        "/api/v1/recommendations/{recommendationId}/read";
    public const string GatewayRecommendationRead =
        "/recommendations/{recommendationId}/read";
    public const string Notifications = "/api/v1/notifications";
    public const string GatewayNotifications = "/notifications";
    public const string NotificationStatus = "/api/v1/notifications/{notificationId}/delivery-status";
    public const string GatewayNotificationStatus = "/notifications/{notificationId}/delivery-status";
}

public static class RecommendationNotificationGatewayHeaders
{
    public const string Authentication = "X-Gateway-Authentication";
    public const string UserId = "X-Gateway-User-Id";
}

public sealed record RecommendationFactResponse(
    string Code,
    decimal Value);

public sealed record RecommendationResponse(
    string RecommendationId,
    string Currency,
    string Code,
    string Severity,
    string Title,
    string Body,
    IReadOnlyList<RecommendationFactResponse> Facts,
    DateTimeOffset GeneratedAtUtc,
    string Status,
    DateTimeOffset StatusChangedAtUtc);

public sealed record RecommendationListResponse(
    string Currency,
    IReadOnlyList<RecommendationResponse> Items);

public sealed record DismissRecommendationRequest(
    DateTimeOffset ChangedAtUtc);

public sealed record MarkRecommendationReadRequest(
    DateTimeOffset ChangedAtUtc);

public sealed record NotificationResponse(
    string NotificationId,
    string RecommendationId,
    string Currency,
    string Channel,
    string TemplateCode,
    string Title,
    string Body,
    string DeliveryStatus,
    DateTimeOffset PreparedAtUtc,
    DateTimeOffset? StatusChangedAtUtc);

public sealed record NotificationListResponse(
    string Currency,
    IReadOnlyList<NotificationResponse> Items);

public sealed record UpdateNotificationDeliveryStatusRequest(
    string DeliveryStatus,
    DateTimeOffset ChangedAtUtc);

public sealed record RecommendationNotificationApiErrorResponse(
    string? Title,
    string? Detail,
    int? Status,
    string? Code,
    string? TraceId);

public sealed record RecommendationNotificationServiceInfoResponse(
    string Service,
    string Status,
    string Environment,
    string StorageProvider,
    string RecommendationSource,
    string DeliveryMode);
