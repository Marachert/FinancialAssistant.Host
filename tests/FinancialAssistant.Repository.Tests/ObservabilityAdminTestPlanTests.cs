using System.Text.Json;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class ObservabilityAdminTestPlanTests
{
    private const string Guide = "docs/engineering/observability-admin-test-plan.md";

    [Fact]
    public void Manifest_IsPlanOnlyAndCannotClaimRuntimeAcceptance()
    {
        using var document = Manifest();
        var root = document.RootElement;
        Assert.Equal(new[] { "classification", "automaticExecution", "runtimeAcceptance", "guide", "cases" },
            root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("synthetic-test-plan-only", root.GetProperty("classification").GetString());
        Assert.False(root.GetProperty("automaticExecution").GetBoolean());
        Assert.False(root.GetProperty("runtimeAcceptance").GetBoolean());
        Assert.Equal(Guide, root.GetProperty("guide").GetString());
    }

    [Fact]
    public void Cases_HaveUniqueIdsOwnedReferencesAndExplicitExecutionLevel()
    {
        using var document = Manifest();
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.Equal(20, cases.Length);
        Assert.Equal(cases.Length, cases.Select(item => item.GetProperty("id").GetString()).Distinct().Count());
        Assert.Equal(new[] { "admin", "alerts", "audit", "health", "jobs", "logs", "mcp", "privacy" },
            cases.Select(item => item.GetProperty("area").GetString()).Distinct().Order());
        var guide = Read(Guide);
        foreach (var item in cases)
        {
            Assert.Equal(new[] { "id", "area", "level", "status", "reference" }, item.EnumerateObject().Select(property => property.Name));
            Assert.Equal("planned", item.GetProperty("status").GetString());
            Assert.Contains(item.GetProperty("level").GetString(), new[] { "offline", "approved-host" });
            var id = item.GetProperty("id").GetString()!;
            Assert.Matches("^OBS-[A-Z]{3}-[0-9]{2}$", id);
            Assert.Contains($"| {id} |", guide, StringComparison.Ordinal);
            var reference = item.GetProperty("reference").GetString()!;
            Assert.DoesNotContain("..", reference, StringComparison.Ordinal);
            Assert.DoesNotContain(":", reference, StringComparison.Ordinal);
            Assert.False(Path.IsPathRooted(reference));
            Assert.NotEmpty(Read(reference));
        }
    }

    [Fact]
    public void Plan_CoversNegativeAdminPrivacyAndMissingEvidence()
    {
        var guide = Read(Guide);
        foreach (var phrase in new[]
        {
            "forged admin role", "spoofed gateway trust", "Late200/401/403",
            "Old response cannot contaminate", "wrong service trust", "unsupported role may return401",
            "synthetic canaries", "capture each relevant output/sink", "JSON escaping",
            "pseudonymous hashes", "not anonymous", "public test",
            "random monitoring UUID/revision are not domain authorization/attempt counts",
            "maximum-spend approval", "no automatic retries"
        })
        {
            Assert.Contains(phrase, guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Plan_DistinguishesSourceCoverageFromExecutionAndRelease()
    {
        var guide = Read(Guide);
        foreach (var phrase in new[]
        {
            "not a report", "existing partial automated coverage", "case ID, exact head/build/environment",
            "pass/fail/blocked/not-run", "not passed", "FIN-200", "FIN-205", "FIN-215", "FIN-216",
            "20/20 failures over5minutes", "19/19 and zero traffic", "restore durable state", "no automatic retries"
        })
        {
            Assert.Contains(phrase, guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Plan_IsLinkedFromOwnedEntryPoints()
    {
        foreach (var path in new[]
        {
            "docs/README.md", "docs/engineering/backend-release-test-suite.md",
            "docs/engineering/failed-job-support-workflow.md", "docs/engineering/alerting-and-incident-priorities.md"
        })
        {
            Assert.Contains("observability-admin-test-plan.md", Read(path), StringComparison.Ordinal);
        }
    }

    private static JsonDocument Manifest() => JsonDocument.Parse(Read("docs/engineering/observability-admin-test-plan.json"));

    private static string Read(string path)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
                {
                    return File.ReadAllText(Path.Combine(directory.FullName, path.Replace('/', Path.DirectorySeparatorChar)));
                }
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
