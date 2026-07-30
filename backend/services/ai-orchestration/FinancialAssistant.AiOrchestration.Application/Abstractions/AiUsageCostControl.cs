namespace FinancialAssistant.AiOrchestration.Application.Abstractions;

public sealed record AiUsageCostControlPolicy
{
    public AiUsageCostControlPolicy(
        int perUserDailyRequestLimit,
        int maximumRequestCharacters,
        decimal monthlyBudgetAlertUsd,
        bool adminVisibilityEnabled)
    {
        if (perUserDailyRequestLimit is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(perUserDailyRequestLimit),
                "The per-user daily AI request limit must be between 1 and 10,000.");
        }

        if (maximumRequestCharacters is < 1 or > 100_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumRequestCharacters),
                "The AI request character limit must be between 1 and 100,000.");
        }

        if (monthlyBudgetAlertUsd is < 1 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(monthlyBudgetAlertUsd),
                "The monthly AI budget alert must be between 1 and 1,000,000 USD.");
        }

        if (!adminVisibilityEnabled)
        {
            throw new ArgumentException(
                "AI usage cost controls require aggregate admin visibility.",
                nameof(adminVisibilityEnabled));
        }

        PerUserDailyRequestLimit = perUserDailyRequestLimit;
        MaximumRequestCharacters = maximumRequestCharacters;
        MonthlyBudgetAlertUsd = monthlyBudgetAlertUsd;
        AdminVisibilityEnabled = adminVisibilityEnabled;
    }

    public int PerUserDailyRequestLimit { get; }

    public int MaximumRequestCharacters { get; }

    public decimal MonthlyBudgetAlertUsd { get; }

    public bool AdminVisibilityEnabled { get; }
}

public interface IAiUsageLimiter
{
    bool TryAcquire(
        string usageSubjectId,
        string providerName,
        DateOnly utcDate,
        int dailyLimit);
}
