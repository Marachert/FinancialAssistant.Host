namespace FinancialAssistant.ReceiptProcessing.Application.Abstractions;

public sealed record OcrUsageCostControlPolicy
{
    public OcrUsageCostControlPolicy(
        int perUserDailyRequestLimit,
        long maximumProviderRequestBytes,
        decimal monthlyBudgetAlertUsd,
        bool adminVisibilityEnabled)
    {
        if (perUserDailyRequestLimit is < 1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(perUserDailyRequestLimit),
                "The per-user daily OCR request limit must be between 1 and 10,000.");
        }

        if (maximumProviderRequestBytes is < 1 or > ReceiptProcessingService.MaximumReceiptSizeBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumProviderRequestBytes),
                $"The OCR provider request limit must be between 1 and {ReceiptProcessingService.MaximumReceiptSizeBytes} bytes.");
        }

        if (monthlyBudgetAlertUsd is < 1 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(monthlyBudgetAlertUsd),
                "The monthly OCR budget alert must be between 1 and 1,000,000 USD.");
        }

        if (!adminVisibilityEnabled)
        {
            throw new ArgumentException(
                "OCR usage cost controls require aggregate admin visibility.",
                nameof(adminVisibilityEnabled));
        }

        PerUserDailyRequestLimit = perUserDailyRequestLimit;
        MaximumProviderRequestBytes = maximumProviderRequestBytes;
        MonthlyBudgetAlertUsd = monthlyBudgetAlertUsd;
        AdminVisibilityEnabled = adminVisibilityEnabled;
    }

    public int PerUserDailyRequestLimit { get; }

    public long MaximumProviderRequestBytes { get; }

    public decimal MonthlyBudgetAlertUsd { get; }

    public bool AdminVisibilityEnabled { get; }
}

public interface IOcrUsageLimiter
{
    bool TryAcquire(
        string userId,
        string providerName,
        DateOnly utcDate,
        int dailyLimit);
}
