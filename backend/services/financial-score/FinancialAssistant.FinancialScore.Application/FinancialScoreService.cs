using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.FinancialScore.Domain;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.FinancialScore.Application;

public sealed class FinancialScoreService
{
    private readonly IFinancialScoreStore store;
    private readonly IFinancialScoreEventPublisher publisher;
    private readonly FinancialScoreCalculator calculator;
    private readonly SemaphoreSlim applyGate = new(1, 1);

    public FinancialScoreService(
        IFinancialScoreStore store,
        IFinancialScoreEventPublisher publisher,
        FinancialScoreCalculator calculator)
    {
        this.store = store;
        this.publisher = publisher;
        this.calculator = calculator;
    }

    public async Task<FinancialScoreCalculation?> ApplyAsync(
        IntegrationEventEnvelope<FinancialRecordChangedV1> envelope,
        IReadOnlyList<FinancialScoreSemanticFactor>? semanticFactors,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        _ = FinancialScoreCalculator.CalculateSemanticAdjustment(semanticFactors);
        await applyGate.WaitAsync(cancellationToken);
        try
        {
            Validate(envelope);
            var replayCalculations = await store.GetBySourceEventIdAsync(
                envelope.EventId,
                cancellationToken);
            if (replayCalculations.Count > 0)
            {
                foreach (var replayCalculation in replayCalculations)
                {
                    await publisher.PublishAsync(
                        MapEvent(envelope, replayCalculation),
                        cancellationToken);
                }

                return replayCalculations.FirstOrDefault(item =>
                    item.Currency == envelope.Payload.Currency.ToUpperInvariant()) ??
                    replayCalculations[0];
            }

            var projection = MapProjection(envelope);
            var writeOutcome = await store.UpsertProjectionAsync(projection, cancellationToken);
            if (writeOutcome.Result == FinancialScoreProjectionWriteResult.Stale)
            {
                return null;
            }

            if (writeOutcome.Result == FinancialScoreProjectionWriteResult.Duplicate)
            {
                throw new InvalidOperationException(
                        "A duplicate financial event has no stored score calculation.");
            }

            var currencies = new[]
            {
                writeOutcome.PreviousCurrency,
                envelope.Payload.Currency.ToUpperInvariant()
            }
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .Cast<string>()
                .ToArray();
            var calculations = new List<FinancialScoreCalculation>(currencies.Length);
            foreach (var currency in currencies)
            {
                var snapshot = await store.GetSnapshotAsync(
                    envelope.UserIdHash!,
                    currency,
                    cancellationToken);
                var calculation = calculator.Calculate(
                    CreateDeterministicId("score", $"{envelope.EventId}|{currency}"),
                    envelope.EventId,
                    envelope.UserIdHash!,
                    currency,
                    snapshot.Records,
                    semanticFactors,
                    envelope.Payload.ChangedAtUtc);
                await store.SaveCalculationAsync(calculation, cancellationToken);
                await publisher.PublishAsync(MapEvent(envelope, calculation), cancellationToken);
                calculations.Add(calculation);
            }

            return calculations.Single(item =>
                item.Currency == envelope.Payload.Currency.ToUpperInvariant());
        }
        finally
        {
            applyGate.Release();
        }
    }

    public Task<FinancialScoreCalculation?> GetCurrentAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken) =>
        store.GetCurrentAsync(
            NormalizeRequired(userIdHash, nameof(userIdHash)),
            NormalizeCurrency(currency),
            cancellationToken);

    public Task<IReadOnlyList<FinancialScoreCalculation>> GetHistoryAsync(
        string userIdHash,
        string currency,
        DateTimeOffset? beforeUtc,
        string? beforeCalculationId,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100.");
        }

        if ((beforeUtc is null) != string.IsNullOrWhiteSpace(beforeCalculationId))
        {
            throw new ArgumentException(
                "History cursor timestamp and calculation ID must be supplied together.");
        }

        return store.GetHistoryAsync(
            NormalizeRequired(userIdHash, nameof(userIdHash)),
            NormalizeCurrency(currency),
            beforeUtc?.ToUniversalTime(),
            beforeCalculationId?.Trim(),
            limit + 1,
            cancellationToken);
    }

    private static FinancialScoreRecordProjection MapProjection(
        IntegrationEventEnvelope<FinancialRecordChangedV1> envelope) =>
        new(
            ReadRecordType(envelope.EventType),
            envelope.Payload.RecordId,
            envelope.UserIdHash!,
            envelope.Payload.Amount,
            envelope.Payload.Currency.ToUpperInvariant(),
            envelope.Payload.Date,
            envelope.Payload.Status,
            envelope.Payload.Revision,
            envelope.Payload.ChangedAtUtc.ToUniversalTime(),
            envelope.EventId);

    private static IntegrationEventEnvelope<ScoreCalculatedV1> MapEvent(
        IntegrationEventEnvelope<FinancialRecordChangedV1> source,
        FinancialScoreCalculation calculation) =>
        new(
            calculation.CalculationId,
            CreateDeterministicId("score-occurrence", source.OccurrenceId),
            FinancialScoreEventTypes.ScoreCalculated,
            calculation.CalculatedAtUtc,
            "financial-score-service",
            FinancialScoreEventTypes.SchemaVersion,
            source.CorrelationId,
            source.EventId,
            calculation.UserIdHash,
            new ScoreCalculatedV1(
                calculation.CalculationId,
                calculation.Currency,
                calculation.Score,
                calculation.FormulaVersion,
                calculation.Factors
                    .Select(item => new FinancialScoreFactorV1(item.Code, item.Contribution))
                    .ToArray(),
                calculation.CalculatedAtUtc));

    private static string CreateDeterministicId(string prefix, string value) =>
        $"{prefix}-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()}";

    private static string ReadRecordType(string eventType)
    {
        if (eventType is FinancialRecordEventTypes.IncomeCreated or
            FinancialRecordEventTypes.IncomeUpdated or
            FinancialRecordEventTypes.IncomeArchived or
            FinancialRecordEventTypes.IncomeRestored)
        {
            return FinancialScoreRecordTypes.Income;
        }

        if (eventType is FinancialRecordEventTypes.ExpenseCreated or
            FinancialRecordEventTypes.ExpenseUpdated or
            FinancialRecordEventTypes.ExpenseArchived or
            FinancialRecordEventTypes.ExpenseRestored)
        {
            return FinancialScoreRecordTypes.Expense;
        }

        throw new ArgumentException("Unsupported financial record event type.", nameof(eventType));
    }

    private static void Validate(IntegrationEventEnvelope<FinancialRecordChangedV1> envelope)
    {
        var payload = envelope.Payload;
        if (envelope.SchemaVersion != FinancialRecordEventTypes.SchemaVersion ||
            string.IsNullOrWhiteSpace(envelope.EventId) ||
            string.IsNullOrWhiteSpace(envelope.UserIdHash) ||
            string.IsNullOrWhiteSpace(payload.RecordId) ||
            payload.Amount <= 0m ||
            string.IsNullOrWhiteSpace(payload.Currency) ||
            payload.Currency.Length != 3 ||
            payload.Date == default ||
            payload.Revision < 0 ||
            payload.ChangedAtUtc == default ||
            (payload.Status != FinancialScoreProjectionStatuses.Active &&
             payload.Status != FinancialScoreProjectionStatuses.Archived))
        {
            throw new ArgumentException("Financial record event is invalid.", nameof(envelope));
        }

        _ = ReadRecordType(envelope.EventType);
    }

    private static string NormalizeCurrency(string currency)
    {
        var normalized = NormalizeRequired(currency, nameof(currency)).ToUpperInvariant();
        return normalized.Length == 3
            ? normalized
            : throw new ArgumentException("Currency must use a three-letter code.", nameof(currency));
    }

    private static string NormalizeRequired(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();
}
