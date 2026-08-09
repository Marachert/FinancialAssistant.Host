using FinancialAssistant.FinancialScore.Domain;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.FinancialScore.Application;

public interface IFinancialScoreStore
{
    Task<FinancialScoreProjectionWriteOutcome> UpsertProjectionAsync(
        FinancialScoreRecordProjection projection,
        CancellationToken cancellationToken);

    Task<FinancialScoreSnapshot> GetSnapshotAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken);

    Task SaveCalculationAsync(
        FinancialScoreCalculation calculation,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FinancialScoreCalculation>> GetBySourceEventIdAsync(
        string sourceEventId,
        CancellationToken cancellationToken);

    Task<FinancialScoreCalculation?> GetCurrentAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<FinancialScoreCalculation>> GetHistoryAsync(
        string userIdHash,
        string currency,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        DateTimeOffset? beforeUtc,
        string? beforeCalculationId,
        int limit,
        CancellationToken cancellationToken);
}

public interface IFinancialScoreEventPublisher
{
    Task PublishAsync(
        IntegrationEventEnvelope<ScoreCalculatedV1> envelope,
        CancellationToken cancellationToken);
}
