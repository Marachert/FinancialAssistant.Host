using FinancialAssistant.Analytics.Domain;

namespace FinancialAssistant.Analytics.Application;

public interface IAnalyticsReadModelStore
{
    Task<AnalyticsProjectionWriteOutcome> UpsertIfNewerAsync(
        AnalyticsRecordProjection projection,
        CancellationToken cancellationToken);

    Task<AnalyticsProjectionSnapshot> GetAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);
}

public interface IAnalyticsEventPublisher
{
    Task PublishAsync(
        FinancialAssistant.Shared.Contracts.Events.IntegrationEventEnvelope<
            FinancialAssistant.Shared.Contracts.Events.AnalyticsUpdatedV1> envelope,
        CancellationToken cancellationToken);
}
