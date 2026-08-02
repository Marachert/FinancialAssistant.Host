namespace FinancialAssistant.RecommendationsNotifications.Infrastructure;

public sealed record RecommendationNotificationRetryStep(
    int Attempt,
    int DelayMilliseconds);

public static class RecommendationNotificationRetryPolicy
{
    private static readonly RecommendationNotificationRetryStep[] Steps =
    {
        new(1, 5_000),
        new(2, 30_000),
        new(3, 300_000)
    };

    public static bool TryGetNext(
        int completedAttempts,
        out RecommendationNotificationRetryStep step)
    {
        if (completedAttempts >= 0 && completedAttempts < Steps.Length)
        {
            step = Steps[completedAttempts];
            return true;
        }

        step = null!;
        return false;
    }

    public static string CreateQueueName(
        string queue,
        RecommendationNotificationRetryStep step,
        string routingKey) =>
        $"{queue}.retry-{step.Attempt}.{routingKey.Replace('.', '-')}";
}
