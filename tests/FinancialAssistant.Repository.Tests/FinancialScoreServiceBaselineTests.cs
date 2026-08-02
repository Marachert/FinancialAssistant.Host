using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class FinancialScoreServiceBaselineTests
{
    [Fact]
    public void Solution_ContainsCompleteFinancialScoreServiceLayers()
    {
        var root = FindRepositoryRoot();
        var solution = File.ReadAllText(Path.Combine(root, "FinancialAssistant.Backend.sln"));

        foreach (var layer in new[] { "Api", "Application", "Contracts", "Domain", "Infrastructure", "Tests" })
        {
            var project = $"FinancialAssistant.FinancialScore.{layer}";
            Assert.Contains(project, solution, StringComparison.Ordinal);
            Assert.True(
                File.Exists(
                    Path.Combine(
                        root,
                        "backend",
                        "services",
                        "financial-score",
                        project,
                        $"{project}.csproj")),
                $"Missing {project} project.");
        }
    }

    [Fact]
    public void Formula_IsVersionedBoundedAndBackendOwned()
    {
        var domain = Read(
            "backend/services/financial-score/FinancialAssistant.FinancialScore.Domain/FinancialScoreModels.cs");
        var calculator = Read(
            "backend/services/financial-score/FinancialAssistant.FinancialScore.Domain/FinancialScoreCalculator.cs");

        Assert.Contains("financial-score-v1", domain, StringComparison.Ordinal);
        Assert.Contains("MaximumSemanticAdjustment = 5m", domain, StringComparison.Ordinal);
        Assert.Contains("MaximumSemanticFactorAdjustment = 2m", domain, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(raw", calculator, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenAI", calculator, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("HttpClient", calculator, StringComparison.Ordinal);
    }

    [Fact]
    public void Service_ConsumesConfirmedEventsPublishesScoreAndExposesHistory()
    {
        var service = Read(
            "backend/services/financial-score/FinancialAssistant.FinancialScore.Application/FinancialScoreService.cs");
        var consumer = Read(
            "backend/services/financial-score/FinancialAssistant.FinancialScore.Infrastructure/FinancialScoreFinancialEventConsumer.cs");
        var contracts = Read(
            "backend/services/financial-score/FinancialAssistant.FinancialScore.Contracts/FinancialScoreContracts.cs");
        var sharedEvent = Read(
            "backend/shared/contracts/FinancialAssistant.Shared.Contracts/Events/ScoreCalculatedV1.cs");

        Assert.Contains("FinancialRecordChangedV1", service, StringComparison.Ordinal);
        Assert.Contains("BasicConsumeAsync", consumer, StringComparison.Ordinal);
        Assert.Contains("fa.financial-score.financial-events.v1", Read(
            "backend/services/financial-score/FinancialAssistant.FinancialScore.Infrastructure/FinancialScoreServiceOptions.cs"), StringComparison.Ordinal);
        Assert.Contains("score.calculated.v1", sharedEvent, StringComparison.Ordinal);
        Assert.Contains("History", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("UserIdHash", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("SourceEventId", contracts, StringComparison.Ordinal);
    }

    [Fact]
    public void FinancialScoreDocumentation_IsLinkedFromIndexes()
    {
        Assert.Contains("docs/engineering/financial-score-v1.md", Read("docs/README.md"), StringComparison.Ordinal);
        Assert.Contains("docs/api/financial-score-v1.md", Read("docs/README.md"), StringComparison.Ordinal);
        Assert.Contains("financial-score-v1.md", Read("docs/api/README.md"), StringComparison.Ordinal);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(
            Path.Combine(
                FindRepositoryRoot(),
                relativePath.Replace('/', Path.DirectorySeparatorChar)));

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
