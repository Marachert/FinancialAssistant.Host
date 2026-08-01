using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class DeveloperOnboardingDocumentationTests
{
    [Fact]
    public void RootReadme_IsTheDeveloperEntryPoint()
    {
        var repositoryRoot = FindRepositoryRoot();
        var readme = ReadRequiredFile(repositoryRoot, "README.md");
        var requiredPhrases = new[]
        {
            "docs/delivery/developer-onboarding.md",
            "docs/delivery/local-developer-smoke-test.md",
            "FinancialAssistant.Backend.sln",
            "infra/docker-compose/README.md",
            "docs/engineering/contributing.md",
            "docs/engineering/ci.md",
            "backend/gateways/",
            "mobile/app-react-native/",
            "web-admin/monitoring-ui/",
            "backend deterministic logic is authoritative",
            "OCR and LLM output is probabilistic input",
            "Resolve review threads only after the final updated CI run is green"
        };

        foreach (var phrase in requiredPhrases)
        {
            Assert.Contains(phrase, readme, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void DeveloperOnboardingGuide_CoversFirstDayWorkflow()
    {
        var repositoryRoot = FindRepositoryRoot();
        var onboarding = ReadRequiredFile(repositoryRoot, "docs/delivery/developer-onboarding.md");
        var requiredPhrases = new[]
        {
            "Required tools",
            "docs/delivery/local-developer-smoke-test.md",
            "docker compose config",
            "Copy-Item .env.example .env",
            "dotnet restore FinancialAssistant.Backend.sln",
            "dotnet test FinancialAssistant.Backend.sln",
            "Public API Gateway",
            "Understand service ownership before coding",
            "Pull request and review expectations",
            "Security and privacy checklist",
            "Onboarding completion checklist"
        };

        foreach (var phrase in requiredPhrases)
        {
            Assert.Contains(phrase, onboarding, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("backend/FinancialAssistant.sln", onboarding, StringComparison.Ordinal);
    }

    [Fact]
    public void LocalDeveloperSmokeTest_CoversPlatformFoundation()
    {
        var repositoryRoot = FindRepositoryRoot();
        var smokeTest = ReadRequiredFile(
            repositoryRoot,
            "docs/delivery/local-developer-smoke-test.md");
        var requiredPhrases = new[]
        {
            "git status --short",
            "docker compose config --quiet",
            "bash scripts/healthcheck.sh",
            "dotnet restore FinancialAssistant.Backend.sln",
            "dotnet test FinancialAssistant.Backend.sln",
            "dotnet format FinancialAssistant.Backend.sln",
            "backend/templates/service-template/README.md",
            "FinancialAssistant.PublicApiGateway.csproj",
            "curl -fsS http://127.0.0.1:5080/health",
            "infra/docker-compose/README.md#troubleshooting",
            "must not use production secrets"
        };

        foreach (var phrase in requiredPhrases)
        {
            Assert.Contains(phrase, smokeTest, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ContributorGuide_UsesCurrentSolutionAndReviewWorkflow()
    {
        var repositoryRoot = FindRepositoryRoot();
        var contributing = ReadRequiredFile(repositoryRoot, "docs/engineering/contributing.md");
        var requiredPhrases = new[]
        {
            "docs/delivery/developer-onboarding.md",
            "FinancialAssistant.Backend.sln",
            "Processing review comments",
            "resolve the thread only after the final pipeline is green",
            "unresolved review threads are zero",
            "Do not commit `.env`"
        };

        foreach (var phrase in requiredPhrases)
        {
            Assert.Contains(phrase, contributing, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("backend/FinancialAssistant.sln", contributing, StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentationIndex_LinksOnboardingEntryPoint()
    {
        var repositoryRoot = FindRepositoryRoot();
        var documentationIndex = ReadRequiredFile(repositoryRoot, "docs/README.md");

        Assert.Contains("docs/delivery/developer-onboarding.md", documentationIndex, StringComparison.Ordinal);
        Assert.Contains("docs/delivery/local-developer-smoke-test.md", documentationIndex, StringComparison.Ordinal);
        Assert.Contains("README.md", documentationIndex, StringComparison.Ordinal);
        Assert.Contains("docs/engineering/contributing.md", documentationIndex, StringComparison.Ordinal);
        Assert.Contains("docs/engineering/ci.md", documentationIndex, StringComparison.Ordinal);
    }

    [Fact]
    public void BackendCi_AlwaysRunsForOnboardingDocumentationChanges()
    {
        var repositoryRoot = FindRepositoryRoot();
        var workflow = ReadRequiredFile(repositoryRoot, ".github/workflows/backend-ci.yml");

        AssertBackendCiAlwaysRunsForMainAndDevelop(workflow);
    }

    [Fact]
    public void OnboardingReferencedFiles_Exist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var referencedFiles = new[]
        {
            "README.md",
            "FinancialAssistant.Backend.sln",
            "infra/docker-compose/README.md",
            "docs/README.md",
            "docs/delivery/developer-onboarding.md",
            "docs/delivery/local-developer-smoke-test.md",
            "docs/engineering/contributing.md",
            "docs/engineering/ci.md",
            ".github/workflows/backend-ci.yml"
        };

        foreach (var path in referencedFiles)
        {
            Assert.True(
                File.Exists(ToRepositoryPath(repositoryRoot, path)),
                $"Onboarding references missing repository file '{path}'.");
        }
    }

    private static string ReadRequiredFile(string repositoryRoot, string path)
    {
        var fullPath = ToRepositoryPath(repositoryRoot, path);
        Assert.True(File.Exists(fullPath), $"Required onboarding file '{path}' is missing.");
        return File.ReadAllText(fullPath);
    }

    private static void AssertBackendCiAlwaysRunsForMainAndDevelop(string workflow)
    {
        var normalizedWorkflow = workflow.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("pull_request:", normalizedWorkflow, StringComparison.Ordinal);
        Assert.Contains("push:", normalizedWorkflow, StringComparison.Ordinal);
        Assert.Contains("pull_request:\n    branches:\n      - main\n      - develop", normalizedWorkflow, StringComparison.Ordinal);
        Assert.Contains("push:\n    branches:\n      - main\n      - develop", normalizedWorkflow, StringComparison.Ordinal);
        Assert.DoesNotContain("paths:", normalizedWorkflow, StringComparison.Ordinal);
    }

    private static string ToRepositoryPath(string repositoryRoot, string path) =>
        Path.Combine(repositoryRoot, path.Replace('/', Path.DirectorySeparatorChar));

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
