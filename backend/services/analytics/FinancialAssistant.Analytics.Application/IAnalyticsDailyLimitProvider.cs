namespace FinancialAssistant.Analytics.Application;

public interface IAnalyticsDailyLimitProvider
{
    Task<decimal?> GetDailyExpenseLimitAsync(
        string userIdHash,
        string currency,
        DateOnly referenceDate,
        CancellationToken cancellationToken);
}
