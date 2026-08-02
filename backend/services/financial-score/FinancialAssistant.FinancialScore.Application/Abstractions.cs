using FinancialAssistant.FinancialScore.Domain;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.FinancialScore.Application;

public interface IFinancialScoreStore
{
    Task<FinancialScoreProjectionWriteResult> UpsertProjectionAsync(
        FinancialScoreRecordProjection projection,
        CancellationToken cancellationToken);

    Task<FinancialScoreSnapshot> GetSnapshotAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken);

    Task SaveCalculationAsync(
        FinancialScoreCalculation calculation,
        CancellationToken cancellationToken);

    Task<FinancialScoreCalculation?> GetBySourceEventIdAsync(
        string sourceEventId,
        CancellationToken cancellationToken);

    Task<FinancialScoreCalculation?> GetCurrentAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FinancialScoreCalculation>> GetHistoryAsync(
        string userIdHash,
        string currency,
        DateTimeOffset? beforeUtc,
        int limit,
        CancellationToken cancellationToken);
}

public interface IFinancialScoreEventPublisher
{
    Task PublishAsync(
        IntegrationEventEnvelope<ScoreCalculatedV1> envelope,
        CancellationToken cancellationToken);
}
