namespace FinancialAssistant.RecommendationsNotifications.Domain;

public sealed class NotificationTemplateCatalog
{
    public PreparedNotification Prepare(
        FinancialRecommendation recommendation,
        string channel,
        string sourceEventId,
        DateTimeOffset preparedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        if (channel is not (NotificationChannels.Push or NotificationChannels.Web))
        {
            throw new ArgumentException("Unsupported notification channel.", nameof(channel));
        }

        var body = channel == NotificationChannels.Push && recommendation.Body.Length > 180
            ? recommendation.Body[..177] + "..."
            : recommendation.Body;
        return new PreparedNotification(
            RecommendationGenerator.StableId(
                "notification",
                recommendation.RecommendationId,
                channel),
            recommendation.RecommendationId,
            recommendation.UserIdHash,
            recommendation.Currency,
            channel,
            $"recommendation-{recommendation.Code}-v1",
            recommendation.Title,
            body,
            NotificationDeliveryStatuses.Prepared,
            sourceEventId,
            preparedAtUtc.ToUniversalTime(),
            null);
    }
}
