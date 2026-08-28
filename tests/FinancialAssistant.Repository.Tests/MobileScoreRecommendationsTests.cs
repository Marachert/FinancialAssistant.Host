using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class MobileScoreRecommendationsTests
{
    [Fact]
    public void InsightsApi_UsesAuthoritativeHistoryAndLifecycleContracts()
    {
        var api = ReadRequiredFile("mobile/app-react-native/src/features/insights/insightsApi.ts");
        var provider = ReadRequiredFile("mobile/app-react-native/src/features/insights/InsightsProvider.tsx");

        Assert.Contains("/financial-score/history?currency=", api, StringComparison.Ordinal);
        Assert.Contains("/recommendations/${query(recommendationId)}/read", api, StringComparison.Ordinal);
        Assert.Contains("/recommendations/${query(recommendationId)}/dismissal", api, StringComparison.Ordinal);
        Assert.Contains("setScoreHistory(historyResult.value.items)", provider, StringComparison.Ordinal);
        Assert.Contains("markRecommendationRead", provider, StringComparison.Ordinal);
        Assert.Contains("dismissRecommendation", provider, StringComparison.Ordinal);
    }

    [Fact]
    public void ScoreScreen_ShowsBackendHistoryWithoutClientFinancialCalculation()
    {
        var score = ReadRequiredFile("mobile/app-react-native/src/app/(app)/score.tsx");

        Assert.Contains("Recent score trend", score, StringComparison.Ordinal);
        Assert.Contains("Backend-calculated snapshots", score, StringComparison.Ordinal);
        Assert.Contains("scoreHistory", score, StringComparison.Ordinal);
        Assert.Contains("accessibilityLabel={`${date}, score ${item.score} out of 100`}", score, StringComparison.Ordinal);
        Assert.DoesNotContain("incomeTotal", score, StringComparison.Ordinal);
        Assert.DoesNotContain("expenseTotal", score, StringComparison.Ordinal);
    }

    [Fact]
    public void Recommendations_ProvideDetailSafeActionsAndCompleteStates()
    {
        var list = ReadRequiredFile("mobile/app-react-native/src/app/(app)/recommendations.tsx");
        var detail = ReadRequiredFile(
            "mobile/app-react-native/src/app/(app)/recommendations/[recommendationId].tsx");

        Assert.Contains("pathname: '/recommendations/[recommendationId]'", list, StringComparison.Ordinal);
        Assert.Contains("Why you are seeing this", detail, StringComparison.Ordinal);
        Assert.Contains("Suggested next step", detail, StringComparison.Ordinal);
        Assert.Contains("actionRoutes[recommendation.explanation.action.code]", detail, StringComparison.Ordinal);
        Assert.Contains("Mark as read", detail, StringComparison.Ordinal);
        Assert.Contains("Dismiss recommendation", detail, StringComparison.Ordinal);
        Assert.Contains("Loading recommendation detail", detail, StringComparison.Ordinal);
        Assert.Contains("Recommendation unavailable", detail, StringComparison.Ordinal);
        Assert.Contains("friendlyApiError", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("router.push(recommendation.explanation.action.route)", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("bad", detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("failure", detail, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadRequiredFile(string path)
    {
        var repositoryRoot = FindRepositoryRoot();
        var fullPath = Path.Combine(
            repositoryRoot,
            path.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Required FIN-183 file '{path}' is missing.");
        return File.ReadAllText(fullPath);
    }

    private static string FindRepositoryRoot()
    {
        foreach (var startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root containing FinancialAssistant.Backend.sln.");
    }
}
