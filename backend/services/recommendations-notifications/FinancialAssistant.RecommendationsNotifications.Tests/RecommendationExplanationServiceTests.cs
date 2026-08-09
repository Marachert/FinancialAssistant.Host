using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.RecommendationsNotifications.Infrastructure;
using FinancialAssistant.Shared.Contracts.Events;
using Xunit;

namespace FinancialAssistant.RecommendationsNotifications.Tests;

public sealed class RecommendationExplanationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 9, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateAsync_UsesDeterministicFallbackWhenProviderIsUnavailable()
    {
        var recommendation = Recommendation(
            "monthly-budget-nearing-limit",
            [new RecommendationFact("monthly-budget-used-percent", 85m)]);
        var service = new RecommendationExplanationService(
            new UnavailableRecommendationExplanationWordingProvider());

        var explanation = await service.CreateAsync(
            recommendation,
            CancellationToken.None);

        Assert.Equal(
            "recommendations.monthly-budget-nearing-limit.explanation",
            explanation.LocalizationKey);
        Assert.Equal(RecommendationExplanationConfidences.High, explanation.Confidence);
        Assert.Equal("review-limits", explanation.Action.Code);
        Assert.Equal("/settings/limits", explanation.Action.Route);
        Assert.Contains("confirmed monthly spending", explanation.Text, StringComparison.Ordinal);
        Assert.False(explanation.IsWordingEnhanced);
    }

    [Fact]
    public async Task CreateAsync_AllowsProviderToImproveOnlyBoundedDisplayText()
    {
        var recommendation = Recommendation(
            "score-recovery",
            [new RecommendationFact("score", 42m)]);
        var provider = new RecordingExplanationWordingProvider(
            new RecommendationExplanationWording(
                "Your deterministic score indicates which factor to review first."));
        var service = new RecommendationExplanationService(provider);

        var explanation = await service.CreateAsync(
            recommendation,
            CancellationToken.None);

        Assert.Equal(recommendation.Code, provider.Input!.RecommendationCode);
        Assert.Equal(recommendation.Facts, provider.Input.Facts);
        Assert.Equal("review-score", explanation.Action.Code);
        Assert.Equal("/score", explanation.Action.Route);
        Assert.Equal(RecommendationExplanationConfidences.High, explanation.Confidence);
        Assert.True(explanation.IsWordingEnhanced);
    }

    [Fact]
    public async Task CreateAsync_FallsBackWhenProviderFailsOrReturnsUnsafeText()
    {
        var recommendation = Recommendation("steady-course", Array.Empty<RecommendationFact>());
        var failed = new RecommendationExplanationService(
            new ThrowingExplanationWordingProvider());
        var unsafeProvider = new RecommendationExplanationService(
            new RecordingExplanationWordingProvider(
                new RecommendationExplanationWording("unsafe\u0001text")));

        var failedResult = await failed.CreateAsync(
            recommendation,
            CancellationToken.None);
        var unsafeResult = await unsafeProvider.CreateAsync(
            recommendation,
            CancellationToken.None);

        Assert.False(failedResult.IsWordingEnhanced);
        Assert.False(unsafeResult.IsWordingEnhanced);
        Assert.Equal(
            RecommendationExplanationConfidences.Baseline,
            failedResult.Confidence);
        Assert.Equal(failedResult.Text, unsafeResult.Text);
    }

    [Fact]
    public async Task RecommendationService_AttachesExplanationBeforePersistenceAndPublication()
    {
        var store = new InMemoryRecommendationNotificationStore();
        var publisher = new RecordingRecommendationPublisher();
        var service = new RecommendationService(
            store,
            new RecommendationGenerator(),
            new PassthroughRecommendationWordingProvider(),
            publisher,
            explanationService: new RecommendationExplanationService(
                new UnavailableRecommendationExplanationWordingProvider()));
        var envelope = new IntegrationEventEnvelope<AnalyticsUpdatedV1>(
            "analytics-explanation-1",
            "analytics-explanation-occurrence-1",
            AnalyticsEventTypes.AnalyticsUpdated,
            Now,
            "financial-assistant-analytics-service",
            AnalyticsEventTypes.SchemaVersion,
            "correlation-1",
            "financial-event-1",
            "owner-a",
            new AnalyticsUpdatedV1(
                "USD",
                new DateOnly(2026, 8, 9),
                1_000m,
                900m,
                100m,
                100m,
                "expense.food",
                Now,
                450m,
                0m));

        var generated = await service.ProcessAnalyticsAsync(
            envelope,
            CancellationToken.None);
        var stored = await service.GetAsync(
            "owner-a",
            "USD",
            CancellationToken.None);

        Assert.NotEmpty(generated);
        Assert.All(generated, item => Assert.NotNull(item.Explanation));
        Assert.All(stored, item => Assert.NotNull(item.Explanation));
        Assert.Equal(generated.Count, publisher.Published.Count);
    }

    private static FinancialRecommendation Recommendation(
        string code,
        IReadOnlyList<RecommendationFact> facts) =>
        new(
            "recommendation-1",
            "owner-a",
            "USD",
            code,
            RecommendationSeverities.Information,
            "Synthetic title",
            "Synthetic body",
            facts,
            "source-1",
            Now,
            RecommendationStatuses.Active,
            Now);

    private sealed class RecordingExplanationWordingProvider(
        RecommendationExplanationWording? result)
        : IRecommendationExplanationWordingProvider
    {
        public RecommendationExplanationInput? Input { get; private set; }

        public Task<RecommendationExplanationWording?> ImproveAsync(
            RecommendationExplanationInput input,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Input = input;
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingExplanationWordingProvider
        : IRecommendationExplanationWordingProvider
    {
        public Task<RecommendationExplanationWording?> ImproveAsync(
            RecommendationExplanationInput input,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Synthetic provider outage.");
    }

    private sealed class PassthroughRecommendationWordingProvider
        : IRecommendationWordingProvider
    {
        public Task<RecommendationWording> CreateAsync(
            FinancialRecommendation recommendation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                new RecommendationWording(
                    recommendation.Title,
                    recommendation.Body));
        }
    }

    private sealed class RecordingRecommendationPublisher
        : IRecommendationEventPublisher
    {
        private readonly List<IntegrationEventEnvelope<RecommendationGeneratedV1>> published = [];

        public IReadOnlyList<IntegrationEventEnvelope<RecommendationGeneratedV1>> Published =>
            published;

        public Task PublishAsync(
            IntegrationEventEnvelope<RecommendationGeneratedV1> envelope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            published.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
