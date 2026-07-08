using System.Text.Json;
using System.Text.RegularExpressions;

namespace FinancialAssistant.Identity.Tests;

public sealed class GatewayIdentityValidationChecklistTests
{
    private const string ChecklistPath = "docs/engineering/gateway-identity-validation-checklist.json";
    private const string DocumentationPath = "docs/engineering/gateway-identity-validation-checklist.md";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void Checklist_IsMachineReadableAndCoversRequiredDomains()
    {
        var checklist = ReadChecklist();
        var expectedDomains = new[]
        {
            "account",
            "provider",
            "route",
            "session",
            "throttling"
        };

        Assert.Equal(1, checklist.SchemaVersion);
        Assert.Equal("gateway-identity-validation-v1", checklist.ChecklistId);
        Assert.Equal(expectedDomains, checklist.RequiredDomains);
        Assert.NotEmpty(checklist.Owners);
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
                    "^(GATEWAY|IDENTITY)-(ROUTE|ACCOUNT|SESSION|PROVIDER|THROTTLING)-[0-9]{3}$",
                    RegexOptions.CultureInvariant),
                check.Id);
            Assert.Contains(check.Domain, expectedDomains);
            Assert.Contains(check.Component, new[] { "public-api-gateway", "identity-service" });
            Assert.False(string.IsNullOrWhiteSpace(check.Intent));
            Assert.False(string.IsNullOrWhiteSpace(check.Expected));
            Assert.NotEmpty(check.NegativeCases);
            Assert.All(check.NegativeCases, negativeCase =>
                Assert.False(string.IsNullOrWhiteSpace(negativeCase)));
            Assert.Equal("enforced", check.AutomationStatus);
            Assert.NotEmpty(check.Evidence);
        });
    }

    [Fact]
    public void ChecklistEvidence_ReferencesExistingAutomatedTests()
    {
        var repositoryRoot = FindRepositoryRoot();
        var checklist = ReadChecklist();

        foreach (var check in checklist.Checks)
        {
            foreach (var evidence in check.Evidence)
            {
                Assert.EndsWith(".cs", evidence.Path, StringComparison.Ordinal);
                Assert.Contains(".Tests/", evidence.Path, StringComparison.Ordinal);
                Assert.NotEmpty(evidence.TestMethods);

                var sourcePath = Path.Combine(
                    repositoryRoot,
                    evidence.Path.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(
                    File.Exists(sourcePath),
                    $"Checklist {check.Id} references missing source file '{evidence.Path}'.");

                var source = File.ReadAllText(sourcePath);
                foreach (var testMethod in evidence.TestMethods)
                {
                    Assert.False(string.IsNullOrWhiteSpace(testMethod));
                    Assert.Contains(
                        $"{testMethod}(",
                        source,
                        StringComparison.Ordinal);
                }
            }
        }
    }

    [Fact]
    public void Documentation_ContainsEveryStableChecklistId()
    {
        var repositoryRoot = FindRepositoryRoot();
        var checklist = ReadChecklist();
        var documentation = File.ReadAllText(Path.Combine(
            repositoryRoot,
            DocumentationPath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains(ChecklistPath, documentation, StringComparison.Ordinal);
        foreach (var check in checklist.Checks)
        {
            Assert.Contains($"### {check.Id}", documentation, StringComparison.Ordinal);
        }
    }

    private static ValidationChecklist ReadChecklist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var checklistFile = Path.Combine(
            repositoryRoot,
            ChecklistPath.Replace('/', Path.DirectorySeparatorChar));
        var checklist = JsonSerializer.Deserialize<ValidationChecklist>(
            File.ReadAllText(checklistFile),
            JsonOptions);

        return Assert.IsType<ValidationChecklist>(checklist);
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

    private sealed record ValidationChecklist(
        int SchemaVersion,
        string ChecklistId,
        string Title,
        string[] Owners,
        string[] RequiredDomains,
        ValidationCheck[] Checks);

    private sealed record ValidationCheck(
        string Id,
        string Domain,
        string Component,
        string Intent,
        string Expected,
        string[] NegativeCases,
        string AutomationStatus,
        ValidationEvidence[] Evidence);

    private sealed record ValidationEvidence(
        string Path,
        string[] TestMethods);
}
