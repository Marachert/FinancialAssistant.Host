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

    public PreparedNotification Prepare(
        NotificationTriggerCandidate trigger,
        string channel)
    {
        ArgumentNullException.ThrowIfNull(trigger);
        if (channel is not (NotificationChannels.Push or NotificationChannels.Web))
        {
            throw new ArgumentException("Unsupported notification channel.", nameof(channel));
        }

        var (title, body) = TriggerTemplate(trigger.Code);
        return new PreparedNotification(
            RecommendationGenerator.StableId(
                "notification",
                trigger.TriggerId,
                channel),
            trigger.TriggerId,
            trigger.UserIdHash,
            trigger.Currency,
            channel,
            $"trigger-{trigger.Code}-v1",
            title,
            body,
            NotificationDeliveryStatuses.Prepared,
            trigger.SourceEventId,
            trigger.OccurredAtUtc,
            null);
    }

    private static (string Title, string Body) TriggerTemplate(string code) =>
        code switch
        {
            NotificationTriggerCodes.DailyInputReminder =>
                ("Quick check-in", "Add today's financial activity when convenient."),
            NotificationTriggerCodes.BudgetApproaching =>
                ("Budget update", "Your budget needs attention. Open Financial Assistant for details."),
            NotificationTriggerCodes.BudgetExceeded =>
                ("Budget update", "Your budget needs attention. Open Financial Assistant for details."),
            NotificationTriggerCodes.ScoreImproved =>
                ("Progress update", "Your financial score improved. Open Financial Assistant for details."),
            NotificationTriggerCodes.RecommendationAvailable =>
                ("New recommendation", "A new recommendation is ready in Financial Assistant."),
            NotificationTriggerCodes.ReceiptProcessingCompleted =>
                ("Receipt ready", "Your receipt finished processing. Open Financial Assistant to review it."),
            _ => throw new ArgumentException("Unsupported notification trigger.", nameof(code))
        };
}
