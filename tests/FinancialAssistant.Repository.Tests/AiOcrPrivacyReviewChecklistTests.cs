using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class AiOcrPrivacyReviewChecklistTests
{
    private const string ChecklistPath = "docs/security/ai-ocr-privacy-review-checklist.json";
    private const string DocumentationPath = "docs/security/ai-ocr-privacy-review-checklist.md";
    private const string SecurityIndexPath = "docs/security/README.md";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Checklist_IsMachineReadableAndCoversRequiredPrivacyDomains()
    {
        var checklist = ReadChecklist();
        var expectedDomains = new[]
        {
            "consent_policy",
            "data_minimization",
            "logging",
            "masking_redaction",
            "provider_data_handling",
            "raw_input_storage",
            "test_data"
        };

        Assert.Equal(1, checklist.SchemaVersion);
        Assert.Equal("ai-ocr-privacy-review-v1", checklist.ChecklistId);
        Assert.Equal("FIN-124", checklist.ReleaseReadinessIssueKey);
        Assert.Equal(
            "owner_storage_only_with_opaque_references_and_bounded_retention",
            checklist.RawInputStoragePolicy);
        Assert.Equal(
            new[] { "fail", "not_applicable", "pass" },
            checklist.DecisionValues.Order(StringComparer.Ordinal).ToArray());
        Assert.Equal(
            new[]
            {
                "approver",
                "capability scope",
                "compensating controls or explicit none",
                "decision date",
                "rationale"
            },
            checklist.NotApplicableEvidenceRequirements.Order(StringComparer.Ordinal).ToArray());
        Assert.NotEmpty(checklist.Owners);
        Assert.Equal(
            expectedDomains,
            checklist.RequiredDomains.Order(StringComparer.Ordinal).ToArray());
        Assert.NotEmpty(checklist.Checks);
        Assert.Equal(
            checklist.Checks.Length,
            checklist.Checks.Select(check => check.Id).Distinct(StringComparer.Ordinal).Count());

        foreach (var domain in expectedDomains)
        {
            Assert.Contains(checklist.Checks, check => check.Domain == domain);
        }

        Assert.All(checklist.Checks, check =>
        {
            Assert.Matches(
                new Regex(
                    "^AI-OCR-PRIVACY-[0-9]{3}$",
                    RegexOptions.CultureInvariant),
                check.Id);
            Assert.Contains(check.Domain, expectedDomains);
            Assert.False(string.IsNullOrWhiteSpace(check.Question));
            Assert.False(string.IsNullOrWhiteSpace(check.Expected));
            Assert.NotEmpty(check.RequiredEvidence);
            Assert.All(check.RequiredEvidence, evidence =>
                Assert.False(string.IsNullOrWhiteSpace(evidence)));
            Assert.NotEmpty(check.BlockingConditions);
            Assert.All(check.BlockingConditions, condition =>
                Assert.False(string.IsNullOrWhiteSpace(condition)));
        });
    }

    [Fact]
    public void ProviderReview_RequiresExplicitDataHandlingAnswers()
    {
        var checklist = ReadChecklist();
        var providerReview = Assert.Single(
            checklist.Checks,
            check => check.Id == "AI-OCR-PRIVACY-002");
        var reviewText = string.Join(
            ' ',
            new[]
            {
                providerReview.Question,
                providerReview.Expected,
                string.Join(' ', providerReview.RequiredEvidence),
                string.Join(' ', providerReview.BlockingConditions)
            });

        foreach (var requiredQuestion in new[]
                 {
                     "retention",
                     "training",
                     "subprocessor",
                     "region",
                     "deletion",
                     "incident"
                 })
        {
            Assert.Contains(requiredQuestion, reviewText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Documentation_ContainsEveryStableChecklistIdAndDecisionRule()
    {
        var repositoryRoot = FindRepositoryRoot();
        var checklist = ReadChecklist();
        var documentation = File.ReadAllText(Path.Combine(
            repositoryRoot,
            DocumentationPath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains(ChecklistPath, documentation, StringComparison.Ordinal);
        Assert.Contains("FIN-124", documentation, StringComparison.Ordinal);
        Assert.Contains("Any unresolved `fail`", documentation, StringComparison.Ordinal);
        Assert.Contains(
            "missing substitute evidence for `not_applicable`",
            documentation,
            StringComparison.Ordinal);
        Assert.Contains("opaque references", documentation, StringComparison.OrdinalIgnoreCase);

        foreach (var check in checklist.Checks)
        {
            Assert.Contains($"### {check.Id}", documentation, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SecurityIndex_LinksHumanAndMachineReadableChecklists()
    {
        var repositoryRoot = FindRepositoryRoot();
        var securityIndex = File.ReadAllText(Path.Combine(
            repositoryRoot,
            SecurityIndexPath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains(
            "ai-ocr-privacy-review-checklist.md",
            securityIndex,
            StringComparison.Ordinal);
        Assert.Contains(
            "ai-ocr-privacy-review-checklist.json",
            securityIndex,
            StringComparison.Ordinal);
    }

    private static PrivacyChecklist ReadChecklist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var checklistFile = Path.Combine(
            repositoryRoot,
            ChecklistPath.Replace('/', Path.DirectorySeparatorChar));
        var checklist = JsonSerializer.Deserialize<PrivacyChecklist>(
            File.ReadAllText(checklistFile),
            JsonOptions);

        return Assert.IsType<PrivacyChecklist>(checklist);
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

    private sealed record PrivacyChecklist(
        int SchemaVersion,
        string ChecklistId,
        string Title,
        string[] Owners,
        string ReleaseReadinessIssueKey,
        string RawInputStoragePolicy,
        string[] DecisionValues,
        string[] NotApplicableEvidenceRequirements,
        string[] RequiredDomains,
        PrivacyCheck[] Checks);

    private sealed record PrivacyCheck(
        string Id,
        string Domain,
        string Question,
        string Expected,
        string[] RequiredEvidence,
        string[] BlockingConditions);
}
