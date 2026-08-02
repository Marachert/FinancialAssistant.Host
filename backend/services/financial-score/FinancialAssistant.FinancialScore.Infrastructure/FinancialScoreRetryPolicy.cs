namespace FinancialAssistant.FinancialScore.Infrastructure;

public sealed record FinancialScoreRetryStep(
    int Attempt,
    int DelayMilliseconds,
    string QueueSuffix);

public static class FinancialScoreRetryPolicy
{
    private static readonly FinancialScoreRetryStep[] Steps =
    {
        new(1, 5_000, "5s"),
        new(2, 30_000, "30s"),
        new(3, 300_000, "5m")
    };

    public static bool TryGetNext(int completedAttempts, out FinancialScoreRetryStep step)
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
        string applicationQueue,
        FinancialScoreRetryStep step,
        string eventType) =>
        $"{applicationQueue}.retry.{step.QueueSuffix}.{eventType}";
}
