using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class HealthReadinessBaselineTests
{
    [Fact]
    public void EveryApiHost_UsesTheSharedHealthConvention()
    {
        var root = FindRepositoryRoot();
        var programs = Directory.GetFiles(
                Path.Combine(root, "backend"),
                "Program.cs",
                SearchOption.AllDirectories)
            .Where(path =>
                path.Contains(
                    $"{Path.DirectorySeparatorChar}services{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase)
                || path.Contains(
                    $"{Path.DirectorySeparatorChar}gateways{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase)
                || path.Contains(
                    $"{Path.DirectorySeparatorChar}templates{Path.DirectorySeparatorChar}service-template{Path.DirectorySeparatorChar}",
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(16, programs.Length);
        foreach (var program in programs)
        {
            var source = File.ReadAllText(program);
            Assert.Contains("AddFinancialAssistantHealthChecks()", source, StringComparison.Ordinal);
            Assert.Contains("MapFinancialAssistantHealthEndpoints()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("MapHealthChecks(", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SharedHealthContract_UsesBoundedTechnicalFieldsOnly()
    {
        var source = ReadRequiredFile(
            FindRepositoryRoot(),
            "backend/shared/observability/FinancialAssistant.Shared.Observability/FinancialAssistantHealthExtensions.cs");

        foreach (var phrase in new[]
                 {
                     "\"/health\"",
                     "\"/health/live\"",
                     "\"/health/ready\"",
                     "LiveTag",
                     "ReadyTag",
                     "errorCategory",
                     "check_failed",
                     "no-store"
                 })
        {
            Assert.Contains(phrase, source, StringComparison.Ordinal);
        }

        foreach (var prohibited in new[]
                 {
                     "entry.Value.Description",
                     "entry.Value.Exception.Message",
                     "entry.Value.Data"
                 })
        {
            Assert.DoesNotContain(prohibited, source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ConventionAndDashboardRequirements_AreDocumented()
    {
        var documentation = ReadRequiredFile(
            FindRepositoryRoot(),
            "docs/engineering/service-health-and-readiness.md");

        foreach (var phrase in new[]
                 {
                     "liveness",
                     "readiness",
                     "required dependencies",
                     "degraded",
                     "unavailable",
                     "readiness dashboard",
                     "aggregate-operational-only",
                     "no paid"
                 })
        {
            Assert.Contains(phrase, documentation, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadRequiredFile(string root, string relativePath)
    {
        var fullPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath), $"Required FIN-192 file '{relativePath}' is missing.");
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
