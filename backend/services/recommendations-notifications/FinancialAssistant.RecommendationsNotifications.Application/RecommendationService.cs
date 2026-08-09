using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.RecommendationsNotifications.Application;

public sealed class RecommendationService
{
    private const int MaximumTitleLength = 120;
    private const int MaximumBodyLength = 500;
    private readonly IRecommendationNotificationStore store;
    private readonly RecommendationGenerator generator;
    private readonly IRecommendationWordingProvider wordingProvider;
    private readonly IRecommendationEventPublisher publisher;
    private readonly SemaphoreSlim processGate = new(1, 1);

    public RecommendationService(
        IRecommendationNotificationStore store,
        RecommendationGenerator generator,
        IRecommendationWordingProvider wordingProvider,
        IRecommendationEventPublisher publisher)
    {
        this.store = store;
        this.generator = generator;
        this.wordingProvider = wordingProvider;
        this.publisher = publisher;
    }

    public async Task<IReadOnlyList<FinancialRecommendation>> ProcessAnalyticsAsync(
        IntegrationEventEnvelope<AnalyticsUpdatedV1> envelope,
        CancellationToken cancellationToken)
    {
        await processGate.WaitAsync(cancellationToken);
        try
        {
            ValidateEnvelope(envelope, AnalyticsEventTypes.AnalyticsUpdated);
            var payload = envelope.Payload;
            ValidateAnalytics(payload);
            var result = await store.ApplyAnalyticsIfNewAsync(
                envelope.EventId,
                envelope.UserIdHash!,
                NormalizeCurrency(payload.Currency),
                new AnalyticsInsightFacts(
                    payload.ReferenceDate,
                    payload.MonthlyIncomeTotal,
                    payload.MonthlyExpenseTotal,
                    payload.DailyExpenseLimit,
                    payload.DailyExpenseSpent,
                    payload.TopExpenseCategoryId,
                    payload.UpdatedAtUtc.ToUniversalTime()),
                cancellationToken);
            return result.Accepted
                ? await GenerateAndPublishAsync(result.Snapshot, envelope, cancellationToken)
                : Array.Empty<FinancialRecommendation>();
        }
        finally
        {
            processGate.Release();
        }
    }

    public async Task<IReadOnlyList<FinancialRecommendation>> ProcessScoreAsync(
        IntegrationEventEnvelope<ScoreCalculatedV1> envelope,
        CancellationToken cancellationToken)
    {
        await processGate.WaitAsync(cancellationToken);
        try
        {
            ValidateEnvelope(envelope, FinancialScoreEventTypes.ScoreCalculated);
            var payload = envelope.Payload;
            if (payload.Score is < 0 or > 100 ||
                string.IsNullOrWhiteSpace(payload.FormulaVersion) ||
                payload.CalculatedAtUtc == default)
            {
                throw new ArgumentException("Score event payload is invalid.", nameof(envelope));
            }

            var result = await store.ApplyScoreIfNewAsync(
                envelope.EventId,
                envelope.UserIdHash!,
                NormalizeCurrency(payload.Currency),
                new ScoreInsightFacts(
                    payload.Score,
                    payload.FormulaVersion.Trim(),
                    payload.CalculatedAtUtc.ToUniversalTime()),
                cancellationToken);
            return result.Accepted
                ? await GenerateAndPublishAsync(result.Snapshot, envelope, cancellationToken)
                : Array.Empty<FinancialRecommendation>();
        }
        finally
        {
            processGate.Release();
        }
    }

    public Task<IReadOnlyList<FinancialRecommendation>> GetAsync(
        string userIdHash,
        string currency,
        CancellationToken cancellationToken) =>
        store.GetRecommendationsAsync(
            NormalizeRequired(userIdHash, nameof(userIdHash)),
            NormalizeCurrency(currency),
            cancellationToken);

    public Task<FinancialRecommendation?> DismissAsync(
        string userIdHash,
        string recommendationId,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken)
    {
        if (changedAtUtc == default)
        {
            throw new ArgumentException("A dismissal timestamp is required.", nameof(changedAtUtc));
        }

        return store.UpdateRecommendationStatusAsync(
            NormalizeRequired(userIdHash, nameof(userIdHash)),
            NormalizeRequired(recommendationId, nameof(recommendationId)),
            RecommendationStatuses.Dismissed,
            changedAtUtc.ToUniversalTime(),
            cancellationToken);
    }

    private async Task<IReadOnlyList<FinancialRecommendation>> GenerateAndPublishAsync<TPayload>(
        InsightSnapshot snapshot,
        IntegrationEventEnvelope<TPayload> source,
        CancellationToken cancellationToken)
    {
        var generated = generator.Generate(snapshot, source.EventId, source.OccurredAtUtc);
        var recommendations = new List<FinancialRecommendation>(generated.Count);
        foreach (var recommendation in generated)
        {
            var wording = await wordingProvider.CreateAsync(recommendation, cancellationToken);
            var safe = recommendation with
            {
                Title = ValidateWording(wording.Title, MaximumTitleLength, "title"),
                Body = ValidateWording(wording.Body, MaximumBodyLength, "body")
            };
            recommendations.Add(safe);
        }

        await store.ReplaceCurrentRecommendationsAsync(
            snapshot.UserIdHash,
            snapshot.Currency,
            recommendations,
            source.OccurredAtUtc.ToUniversalTime(),
            cancellationToken);
        foreach (var recommendation in recommendations)
        {
            await publisher.PublishAsync(
                new IntegrationEventEnvelope<RecommendationGeneratedV1>(
                    $"recommendation-{recommendation.RecommendationId}",
                    recommendation.RecommendationId,
                    RecommendationEventTypes.RecommendationGenerated,
                    recommendation.GeneratedAtUtc,
                    "financial-assistant-recommendations-service",
                    RecommendationEventTypes.SchemaVersion,
                    source.CorrelationId,
                    source.EventId,
                    snapshot.UserIdHash,
                    new RecommendationGeneratedV1(
                        recommendation.RecommendationId,
                        recommendation.Currency,
                        recommendation.Code,
                        recommendation.Severity,
                        recommendation.Title,
                        recommendation.Body,
                        recommendation.Facts
                            .Select(item => new RecommendationFactV1(item.Code, item.Value))
                            .ToArray(),
                        recommendation.GeneratedAtUtc)),
                cancellationToken);
        }

        await store.MarkInsightEventCompletedAsync(
            source.EventId,
            snapshot.UserIdHash,
            cancellationToken);

        return recommendations;
    }

    private static void ValidateEnvelope<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        string expectedType)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.EventType != expectedType ||
            envelope.SchemaVersion != 1 ||
            string.IsNullOrWhiteSpace(envelope.UserIdHash))
        {
            throw new ArgumentException("Insight event envelope is invalid.", nameof(envelope));
        }
    }

    private static void ValidateAnalytics(AnalyticsUpdatedV1 payload)
    {
        if (payload.ReferenceDate == default ||
            payload.MonthlyIncomeTotal < 0m ||
            payload.MonthlyExpenseTotal < 0m ||
            payload.DailyExpenseSpent < 0m ||
            payload.DailyExpenseLimit is <= 0m ||
            payload.UpdatedAtUtc == default)
        {
            throw new ArgumentException("Analytics event payload is invalid.", nameof(payload));
        }
    }

    private static string NormalizeCurrency(string value)
    {
        var currency = NormalizeRequired(value, nameof(value)).ToUpperInvariant();
        return currency.Length == 3
            ? currency
            : throw new ArgumentException("Currency must use a three-letter code.", nameof(value));
    }

    private static string NormalizeRequired(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", parameterName)
            : value.Trim();

    private static string ValidateWording(string value, int maximumLength, string fieldName)
    {
        var normalized = NormalizeRequired(value, fieldName);
        if (normalized.Length > maximumLength || normalized.Any(char.IsControl))
        {
            throw new InvalidOperationException(
                $"Recommendation {fieldName} is outside the safe wording bounds.");
        }

        return normalized;
    }
}
