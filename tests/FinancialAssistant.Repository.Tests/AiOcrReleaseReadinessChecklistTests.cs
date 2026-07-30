using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class AiOcrReleaseReadinessChecklistTests
{
    private const string ChecklistPath =
        "docs/engineering/ai-ocr-release-readiness-checklist.json";
    private const string DocumentationPath =
        "docs/engineering/ai-ocr-release-readiness-checklist.md";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Checklist_IsMachineReadableAndCoversEveryRequiredReleaseDomain()
    {
        var checklist = ReadChecklist();
        var expectedDomains = new[]
        {
            "cost_controls",
            "fallback_experience",
            "integration_testing",
            "monitoring",
            "privacy_and_consent",
            "provider_configuration",
            "support"
        };

        Assert.Equal(1, checklist.SchemaVersion);
        Assert.Equal("ai-ocr-release-readiness-v1", checklist.ChecklistId);
        Assert.Equal("FIN-124", checklist.JiraIssueKey);
        Assert.Equal(
            "first_public_release_or_app_store_publication",
            checklist.AppliesBefore);
        Assert.Equal(
            new[] { "blocked", "not_applicable", "pass" },
            checklist.DecisionValues.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(
            "all_required_checks_pass_or_have_approved_not_applicable_decisions_and_no_blocking_condition_remains",
            checklist.ReleaseRule);
        Assert.Equal(
            "safe_metadata_only_no_credentials_raw_prompts_provider_responses_receipt_content_ocr_text_pii_or_financial_values",
            checklist.EvidenceSafetyPolicy);
        Assert.Equal(
            expectedDomains,
            checklist.RequiredDomains.Order(StringComparer.Ordinal).ToArray());

        Assert.NotEmpty(checklist.Checks);
        Assert.Equal(
            checklist.Checks.Length,
            checklist.Checks.Select(check => check.Id)
                .Distinct(StringComparer.Ordinal)
                .Count());

        foreach (var domain in expectedDomains)
        {
            Assert.Contains(checklist.Checks, check => check.Domain == domain);
        }

        Assert.All(checklist.Checks, check =>
        {
            Assert.Matches(
                new Regex(
                    "^AI-OCR-READY-[0-9]{3}$",
                    RegexOptions.CultureInvariant),
                check.Id);
            Assert.Contains(check.Domain, expectedDomains);
            Assert.False(string.IsNullOrWhiteSpace(check.Owner));
            Assert.False(string.IsNullOrWhiteSpace(check.Requirement));
            Assert.NotEmpty(check.RequiredEvidence);
            Assert.NotEmpty(check.BlockingConditions);
            Assert.NotEmpty(check.References);
        });
    }

    [Fact]
    public void Dependencies_ReferencePrivacyConfigurationCostAndIntegrationTasks()
    {
        var repositoryRoot = FindRepositoryRoot();
        var checklist = ReadChecklist();
        var expectedDependencies = new Dictionary<string, string>
        {
            ["FIN-117"] = "docs/security/ai-ocr-privacy-review-checklist.md",
            ["FIN-118"] = "docs/engineering/ai-ocr-provider-configuration.md",
            ["FIN-121"] = "docs/engineering/ai-ocr-usage-cost-controls.md",
            ["FIN-123"] = "docs/engineering/ai-ocr-integration-test-plan.md"
        };

        Assert.Equal(
            expectedDependencies.Keys.Order(StringComparer.Ordinal),
            checklist.Dependencies.Select(dependency => dependency.JiraIssueKey)
                .Order(StringComparer.Ordinal));

        Assert.All(checklist.Dependencies, dependency =>
        {
            Assert.Equal(
                expectedDependencies[dependency.JiraIssueKey],
                dependency.Path);
            Assert.False(string.IsNullOrWhiteSpace(dependency.Title));
            Assert.True(
                File.Exists(ToRepositoryPath(repositoryRoot, dependency.Path)),
                $"Dependency path '{dependency.Path}' does not exist.");
        });

        Assert.All(
            checklist.Checks.SelectMany(check => check.References).Distinct(),
            reference => Assert.True(
                File.Exists(ToRepositoryPath(repositoryRoot, reference)),
                $"Evidence reference '{reference}' does not exist."));
    }

    [Fact]
    public void Documentation_DefinesFallbackMonitoringSupportAndExplicitBlockers()
    {
        var repositoryRoot = FindRepositoryRoot();
        var checklist = ReadChecklist();
        var documentation = File.ReadAllText(
            ToRepositoryPath(repositoryRoot, DocumentationPath));

        Assert.Contains(ChecklistPath, documentation, StringComparison.Ordinal);
        Assert.Contains("FIN-117", documentation, StringComparison.Ordinal);
        Assert.Contains("FIN-121", documentation, StringComparison.Ordinal);
        Assert.Contains(
            "Any `blocked` decision",
            documentation,
            StringComparison.Ordinal);
        Assert.Contains(
            "manual draft entry",
            documentation,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "P8 admin and monitoring workstream",
            documentation,
            StringComparison.Ordinal);
        Assert.Contains(
            "Support and troubleshooting",
            documentation,
            StringComparison.Ordinal);
        Assert.Contains(
            "Current Baseline And Explicit Blockers",
            documentation,
            StringComparison.Ordinal);
        Assert.Contains(
            "public-release blockers",
            documentation,
            StringComparison.Ordinal);

        Assert.All(checklist.Checks, check =>
            Assert.Contains($"### {check.Id}", documentation, StringComparison.Ordinal));
    }

    [Fact]
    public void SourceDocuments_LinkTheReleaseReadinessChecklist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var linkedDocuments = new[]
        {
            "backend/services/ai-orchestration/README.md",
            "backend/services/receipt-processing/README.md",
            "docs/engineering/ai-ocr-integration-test-plan.md",
            "docs/engineering/ai-ocr-provider-configuration.md",
            "docs/engineering/ai-ocr-usage-cost-controls.md",
            "docs/security/ai-ocr-privacy-review-checklist.md"
        };

        Assert.All(linkedDocuments, relativePath =>
        {
            var document = File.ReadAllText(
                ToRepositoryPath(repositoryRoot, relativePath));
            Assert.Contains(
                "ai-ocr-release-readiness-checklist.md",
                document,
                StringComparison.Ordinal);
        });
    }

    private static ReleaseReadinessChecklist ReadChecklist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var checklist = JsonSerializer.Deserialize<ReleaseReadinessChecklist>(
            File.ReadAllText(ToRepositoryPath(repositoryRoot, ChecklistPath)),
            JsonOptions);

        return Assert.IsType<ReleaseReadinessChecklist>(checklist);
    }

    private static string ToRepositoryPath(string repositoryRoot, string relativePath) =>
        Path.Combine(
            repositoryRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));

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

    private sealed record ReleaseReadinessChecklist(
        int SchemaVersion,
        string ChecklistId,
        string JiraIssueKey,
        string AppliesBefore,
        string[] DecisionValues,
        string ReleaseRule,
        string EvidenceSafetyPolicy,
        string[] RequiredDomains,
        ReleaseDependency[] Dependencies,
        ReleaseCheck[] Checks);

    private sealed record ReleaseDependency(
        string JiraIssueKey,
        string Title,
        string Path);

    private sealed record ReleaseCheck(
        string Id,
        string Domain,
        string Owner,
        string Requirement,
        string[] RequiredEvidence,
        string[] BlockingConditions,
        string[] References);
}
