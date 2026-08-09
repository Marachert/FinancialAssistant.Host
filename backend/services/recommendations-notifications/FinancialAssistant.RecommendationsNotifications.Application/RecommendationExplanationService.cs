using FinancialAssistant.RecommendationsNotifications.Domain;

namespace FinancialAssistant.RecommendationsNotifications.Application;

public sealed record RecommendationExplanationWording(
    string Text);

public interface IRecommendationExplanationWordingProvider
{
    Task<RecommendationExplanationWording?> ImproveAsync(
        RecommendationExplanationInput input,
        CancellationToken cancellationToken);
}

public sealed class UnavailableRecommendationExplanationWordingProvider
    : IRecommendationExplanationWordingProvider
{
    public Task<RecommendationExplanationWording?> ImproveAsync(
        RecommendationExplanationInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<RecommendationExplanationWording?>(null);
    }
}

public sealed class RecommendationExplanationService
{
    private const int MaximumExplanationLength = 500;
    private readonly IRecommendationExplanationWordingProvider wordingProvider;

    public RecommendationExplanationService(
        IRecommendationExplanationWordingProvider wordingProvider)
    {
        this.wordingProvider = wordingProvider;
    }

    public async Task<RecommendationExplanation> CreateAsync(
        FinancialRecommendation recommendation,
        CancellationToken cancellationToken)
    {
        var input = RecommendationExplanationCatalog.CreateInput(recommendation);
        RecommendationExplanationWording? wording;
        try
        {
            wording = await wordingProvider.ImproveAsync(input, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return RecommendationExplanationCatalog.CreateFallback(recommendation);
        }

        var improvedText = NormalizeSafeText(wording?.Text);
        return improvedText is null
            ? RecommendationExplanationCatalog.CreateFallback(recommendation)
            : new RecommendationExplanation(
                input.LocalizationKey,
                improvedText,
                input.Confidence,
                input.Action,
                true);
    }

    private static string? NormalizeSafeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= MaximumExplanationLength &&
               !normalized.Any(char.IsControl)
            ? normalized
            : null;
    }
}
