using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class AiOcrIntegrationTestPlanTests
{
    private const string PlanPath =
        "docs/engineering/ai-ocr-integration-test-plan.json";
    private const string DocumentationPath =
        "docs/engineering/ai-ocr-integration-test-plan.md";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Plan_IsMachineReadableAndCoversEveryFin123Domain()
    {
        var plan = ReadPlan();
        var expectedDomains = new[]
        {
            "ai_parsing_contract",
            "draft_suggestion_update",
            "low_confidence_suggestion",
            "malformed_provider_response",
            "ocr_extraction_fixture",
            "provider_failure",
            "receipt_normalization"
        };

        Assert.Equal(1, plan.SchemaVersion);
        Assert.Equal("ai-ocr-integration-test-plan-v1", plan.PlanId);
        Assert.Equal("FIN-123", plan.JiraIssueKey);
        Assert.Equal("FIN-124", plan.ReleaseReadinessIssueKey);
        Assert.Equal(
            "generated_synthetic_only_no_real_or_provider_captured_data",
            plan.FixturePolicy);
        Assert.Equal(
            "mocked_by_default_approved_sandbox_contract_runs_only",
            plan.ProviderExecutionPolicy);
        Assert.Equal(
            expectedDomains,
            plan.RequiredDomains.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(expectedDomains.Length, plan.TestCases.Length);
        Assert.Equal(
            expectedDomains,
            plan.TestCases
                .Select(testCase => testCase.Domain)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void Cases_HaveStableIdsSyntheticFixturesNegativeCoverageAndExistingEvidence()
    {
        var repositoryRoot = FindRepositoryRoot();
        var plan = ReadPlan();
        var fixtureIds = plan.Fixtures
            .Select(fixture => fixture.Id)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(plan.Fixtures);
        Assert.Equal(plan.Fixtures.Length, fixtureIds.Count);
        Assert.All(plan.Fixtures, fixture =>
        {
            Assert.Matches(
                new Regex("^SYN-[A-Z0-9-]+$", RegexOptions.CultureInvariant),
                fixture.Id);
            Assert.Equal("generated_for_fin_123", fixture.Provenance);
            Assert.True(fixture.SyntheticOnly);
            Assert.False(string.IsNullOrWhiteSpace(fixture.Kind));
            Assert.False(string.IsNullOrWhiteSpace(fixture.Purpose));
        });

        Assert.Equal(
            plan.TestCases.Length,
            plan.TestCases.Select(testCase => testCase.Id)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.All(plan.TestCases, testCase =>
        {
            Assert.Matches(
                new Regex("^AI-OCR-IT-[0-9]{3}$", RegexOptions.CultureInvariant),
                testCase.Id);
            Assert.Equal("required", testCase.ReleaseGate);
            Assert.NotEmpty(testCase.FixtureIds);
            Assert.All(testCase.FixtureIds, fixtureId =>
                Assert.Contains(fixtureId, fixtureIds));
            Assert.NotEmpty(testCase.Assertions);
            Assert.NotEmpty(testCase.NegativeCases);
            Assert.NotEmpty(testCase.ExistingEvidence);

            foreach (var evidence in testCase.ExistingEvidence)
            {
                var evidencePath = Path.Combine(
                    repositoryRoot,
                    evidence.Path.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(
                    File.Exists(evidencePath),
                    $"Evidence path '{evidence.Path}' does not exist.");
                var source = File.ReadAllText(evidencePath);
                Assert.NotEmpty(evidence.Tests);
                Assert.All(evidence.Tests, testName =>
                    Assert.Contains(testName, source, StringComparison.Ordinal));
            }
        });
    }

    [Fact]
    public void Documentation_ContainsStableCasesFixturesExecutionAndReleaseRules()
    {
        var repositoryRoot = FindRepositoryRoot();
        var plan = ReadPlan();
        var documentation = File.ReadAllText(Path.Combine(
            repositoryRoot,
            DocumentationPath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains(PlanPath, documentation, StringComparison.Ordinal);
        Assert.Contains("FIN-124", documentation, StringComparison.Ordinal);
        Assert.Contains(
            "No test in the required pull-request lane may contact an external provider.",
            documentation,
            StringComparison.Ordinal);
        Assert.Contains(
            "generated-synthetic-only",
            documentation,
            StringComparison.Ordinal);
        Assert.Contains(
            "dotnet test FinancialAssistant.Backend.sln",
            documentation,
            StringComparison.Ordinal);

        Assert.All(plan.Fixtures, fixture =>
            Assert.Contains($"`{fixture.Id}`", documentation, StringComparison.Ordinal));
        Assert.All(plan.TestCases, testCase =>
            Assert.Contains($"### {testCase.Id}", documentation, StringComparison.Ordinal));
    }

    [Fact]
    public void ServiceAndReadinessDocuments_LinkTheIntegrationPlan()
    {
        var repositoryRoot = FindRepositoryRoot();
        var linkedDocuments = new[]
        {
            "backend/services/ai-orchestration/README.md",
            "backend/services/receipt-processing/README.md",
            "docs/engineering/ai-ocr-provider-configuration.md",
            "docs/security/ai-ocr-privacy-review-checklist.md"
        };

        Assert.All(linkedDocuments, relativePath =>
        {
            var document = File.ReadAllText(Path.Combine(
                repositoryRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            Assert.Contains(
                "ai-ocr-integration-test-plan.md",
                document,
                StringComparison.Ordinal);
        });
    }

    private static IntegrationTestPlan ReadPlan()
    {
        var repositoryRoot = FindRepositoryRoot();
        var planFile = Path.Combine(
            repositoryRoot,
            PlanPath.Replace('/', Path.DirectorySeparatorChar));
        var plan = JsonSerializer.Deserialize<IntegrationTestPlan>(
            File.ReadAllText(planFile),
            JsonOptions);

        return Assert.IsType<IntegrationTestPlan>(plan);
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

    private sealed record IntegrationTestPlan(
        int SchemaVersion,
        string PlanId,
        string JiraIssueKey,
        string ReleaseReadinessIssueKey,
        string FixturePolicy,
        string ProviderExecutionPolicy,
        string[] RequiredDomains,
        SyntheticFixture[] Fixtures,
        IntegrationTestCase[] TestCases);

    private sealed record SyntheticFixture(
        string Id,
        string Kind,
        string Provenance,
        bool SyntheticOnly,
        string Purpose);

    private sealed record IntegrationTestCase(
        string Id,
        string Domain,
        string Level,
        string[] FixtureIds,
        string[] Assertions,
        string[] NegativeCases,
        ExistingEvidence[] ExistingEvidence,
        string ReleaseGate);

    private sealed record ExistingEvidence(
        string Path,
        string[] Tests);
}
