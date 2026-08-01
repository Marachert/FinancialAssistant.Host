using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class CiQualityGateTests
{
    [Fact]
    public void BackendCi_DefinesEveryRequiredQualityGate()
    {
        var repositoryRoot = FindRepositoryRoot();
        var workflow = ReadRequiredFile(repositoryRoot, ".github/workflows/backend-ci.yml");

        Assert.Contains("ci-dotnet-build-test:", workflow, StringComparison.Ordinal);
        Assert.Contains("ci-dotnet-format:", workflow, StringComparison.Ordinal);
        Assert.Contains("ci-privacy-baseline:", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "pwsh -NoProfile -NonInteractive -File tools/scripts/verify-privacy-baseline.ps1",
            workflow,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PrivacyBaselineScript_UsesTrackedFilesAndSafeFailureOutput()
    {
        var repositoryRoot = FindRepositoryRoot();
        var script = ReadRequiredFile(repositoryRoot, "tools/scripts/verify-privacy-baseline.ps1");

        Assert.Contains("$ErrorActionPreference = \"Stop\"", script, StringComparison.Ordinal);
        Assert.Contains("ls-files", script, StringComparison.Ordinal);
        Assert.Contains("appsettings.Production.json", script, StringComparison.Ordinal);
        Assert.Contains("private key material", script, StringComparison.Ordinal);
        Assert.Contains(
            "Matched values are intentionally omitted.",
            script,
            StringComparison.Ordinal);
        Assert.Contains("exit 1", script, StringComparison.Ordinal);
    }

    [Fact]
    public void CiPolicy_DocumentsRequiredAndFutureGates()
    {
        var repositoryRoot = FindRepositoryRoot();
        var policy = ReadRequiredFile(repositoryRoot, "docs/engineering/ci.md");

        Assert.Contains("ci-dotnet-build-test", policy, StringComparison.Ordinal);
        Assert.Contains("ci-dotnet-format", policy, StringComparison.Ordinal);
        Assert.Contains("ci-privacy-baseline", policy, StringComparison.Ordinal);
        Assert.Contains("TODO-PRIVACY-SEMANTIC", policy, StringComparison.Ordinal);
        Assert.Contains("TODO-MAPPING-TESTS", policy, StringComparison.Ordinal);
        Assert.Contains("TODO-CONTRACT-TESTS", policy, StringComparison.Ordinal);
    }

    private static string ReadRequiredFile(string repositoryRoot, string path)
    {
        var fullPath = Path.Combine(
            repositoryRoot,
            path.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(fullPath), $"Required CI file '{path}' is missing.");
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
