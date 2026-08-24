using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class MobileSmokeRegressionTestPlanTests
{
    private const string PlanPath = "docs/engineering/mobile-smoke-regression-test-plan.md";

    [Fact]
    public void Plan_CoversRequiredSmokeAndRegressionScenarios()
    {
        var plan = NormalizeWhitespace(ReadRequiredFile(PlanPath));

        for (var index = 1; index <= 18; index++)
        {
            Assert.Contains($"MOB-SMK-{index:000}", plan, StringComparison.Ordinal);
        }

        for (var index = 1; index <= 20; index++)
        {
            Assert.Contains($"MOB-REG-{index:000}", plan, StringComparison.Ordinal);
        }

        foreach (var flow in new[]
                 {
                     "Register",
                     "Onboarding",
                     "Empty dashboard",
                     "Free-form input",
                     "Edit and confirm draft",
                     "Reject draft",
                     "Receipt upload",
                     "Analytics",
                     "Notification inbox",
                     "Offline recovery",
                     "Friendly failures",
                     "Accessibility sanity"
                 })
        {
            Assert.Contains(flow, plan, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Plan_RequiresBothPlatformsAndVisibleReleaseBlockers()
    {
        var plan = NormalizeWhitespace(ReadRequiredFile(PlanPath));

        foreach (var requirement in new[]
                 {
                     "Android emulator",
                     "physical-device pass",
                     "iOS simulator on macOS",
                     "iOS and Android separately",
                     "P0",
                     "P1",
                     "Final decision | Pass / Blocked",
                     "missing platform path",
                     "unresolved actionable review",
                     "exact-head CI",
                     "first-user readiness"
                 })
        {
            Assert.Contains(requirement, plan, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Plan_PreservesPrivacyCostAndAuthorityBoundaries()
    {
        var plan = NormalizeWhitespace(ReadRequiredFile(PlanPath));

        foreach (var requirement in new[]
                 {
                     "synthetic accounts",
                     "Never use production identities",
                     "Do not enable a live paid provider",
                     "Backend-confirmed records",
                     "authoritative",
                     "OCR and LLM output is suggestion input",
                     "Do not attach raw input or receipt bytes",
                     "no token, owner hash, raw phrase, receipt/OCR content"
                 })
        {
            Assert.Contains(requirement, plan, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Plan_IsDiscoverableFromDocumentationAndMobileIndexes()
    {
        var documentationIndex = ReadRequiredFile("docs/README.md");
        var mobileIndex = ReadRequiredFile("mobile/README.md");
        var mobileReadme = ReadRequiredFile("mobile/app-react-native/README.md");

        Assert.Contains(PlanPath, documentationIndex, StringComparison.Ordinal);
        Assert.Contains(PlanPath, mobileIndex, StringComparison.Ordinal);
        Assert.Contains(PlanPath, mobileReadme, StringComparison.Ordinal);
    }

    private static string ReadRequiredFile(string path)
    {
        var repositoryRoot = FindRepositoryRoot();
        var fullPath = Path.Combine(
            repositoryRoot,
            path.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Required mobile test-plan file '{path}' is missing.");
        return File.ReadAllText(fullPath);
    }

    private static string NormalizeWhitespace(string content) =>
        string.Join(' ', content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

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
