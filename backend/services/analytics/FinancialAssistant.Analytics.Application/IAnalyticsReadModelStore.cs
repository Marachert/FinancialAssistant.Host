using FinancialAssistant.Analytics.Domain;

namespace FinancialAssistant.Analytics.Application;

public interface IAnalyticsReadModelStore
{
    Task UpsertIfNewerAsync(
        AnalyticsRecordProjection projection,
        CancellationToken cancellationToken);

    Task<AnalyticsProjectionSnapshot> GetAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);
}
