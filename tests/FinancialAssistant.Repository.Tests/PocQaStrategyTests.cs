using System.Text.RegularExpressions;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class PocQaStrategyTests
{
    private const string Guide = "docs/engineering/poc-qa-strategy.md";

    [Fact]
    public void Strategy_HasUniqueCriticalFlowsAndAllExecutionLanes()
    {
        var guide = Read(Guide);
        var ids = Regex.Matches(guide, @"(?m)^\| (QA-SMK-\d{3}) \|")
            .Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(Enumerable.Range(1, 12).Select(number => $"QA-SMK-{number:000}"), ids);
        foreach (var lane in new[] { "L1 Deterministic", "L2 Service integration", "L3 Connected system", "L4 Client acceptance", "L5 Release rehearsal" })
        {
            Assert.Contains(lane, guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Strategy_ReferencesExistingOwnedPlansAndEntryPoints()
    {
        var guide = Read(Guide);
        var directory = Path.GetDirectoryName(Rooted(Guide))!;
        var links = Regex.Matches(guide, @"\]\(([^)]+\.\w+)\)")
            .Select(match => match.Groups[1].Value).ToArray();
        Assert.NotEmpty(links);
        foreach (var link in links)
        {
            Assert.DoesNotContain(":", link, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.GetFullPath(Path.Combine(directory, link))), link);
        }
        foreach (var path in new[] { "docs/README.md", "docs/engineering/backend-release-test-suite.md", "docs/engineering/mobile-smoke-regression-test-plan.md", "docs/engineering/operational-release-readiness.md" })
        {
            Assert.Contains("poc-qa-strategy.md", Read(path), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Strategy_CannotConflateDefinitionCiAndRuntimeAcceptance()
    {
        var guide = Regex.Replace(Read(Guide), @"\s+", " ");
        foreach (var phrase in new[]
        {
            "plan, not execution evidence", "Not Ready", "Default additional spend is zero",
            "no automatic fallback", "Exactly one authoritative record", "cross-owner",
            "non-sending placeholder is not delivery evidence", "both native platforms",
            "actual browser", "independently verified actual merge", "artifact digest",
            "Pass/Fail/Blocked/Not run", "maximum spend",
            "Required smoke/area gates cannot be waived", "rollback gate pass independently",
            "not the P1-P4 operational incident response classes", "No-Go"
        })
        {
            Assert.Contains(phrase, guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ResultGate_RequiresExplicitPassAndRejectsBlankOrNonPassingResults()
    {
        var guide = Regex.Replace(Read(Guide), @"\s+", " ");
        Assert.Contains("A blank result never counts as Pass", guide, StringComparison.Ordinal);
        Assert.Contains("Only an explicit Pass with valid evidence qualifies", guide, StringComparison.Ordinal);
        Assert.Contains("Fail, Blocked and Not run never qualify", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("No blank result means Pass", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void Strategy_LeavesDownstreamImplementationAndApprovalOwned()
    {
        var guide = Regex.Replace(Read(Guide), @"\s+", " ");
        foreach (var phrase in new[]
        {
            "Unassigned required work is Blocked", "FIN-202", "FIN-203", "FIN-204",
            "FIN-205", "FIN-206/207/208/209", "FIN-210/211", "FIN-212/213",
            "FIN-215", "FIN-216", "FIN-217", "FIN-218",
            "not automatically closed as a duplicate", "not proof of a public web application",
            "never an expected balance, score or confirmed entity", "affected neighboring regressions"
        })
        {
            Assert.Contains(phrase, guide, StringComparison.Ordinal);
        }
    }

    private static string Read(string path) => File.ReadAllText(Rooted(path));

    [Fact]
    public void ProductionLikeScope_CoversAllSevenFin203Areas()
    {
        var guide = Read(Guide);
        var ids = Regex.Matches(guide, @"(?m)^\| (QA-SCOPE-\d{3}) \|")
            .Select(match => match.Groups[1].Value).ToArray();
        Assert.Equal(Enumerable.Range(1, 7).Select(number => $"QA-SCOPE-{number:000}"), ids);
        foreach (var area in new[]
        {
            "Backend unit/integration", "Mobile smoke/regression", "API contracts",
            "AI/OCR fixtures", "Privacy/security", "Windows deployment", "Store readiness"
        })
        {
            Assert.Contains(area, guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ProductionLikeScope_RejectsStaticOnlyAcceptanceAndImplicitApproval()
    {
        var guide = Regex.Replace(Read(Guide), @"\s+", " ");
        foreach (var phrase in new[]
        {
            "All seven areas are required", "not seven recorded passes",
            "actual deployment topology", "OpenAPI path presence alone does not prove",
            "filename/secret scan is not a complete security review", "archive creation alone",
            "actual tester installation", "not that all desired scenarios exist or ran",
            "An unimplemented required case is Blocked", "does not authorize Docker startup",
            "Passing the seven scope areas alone is not the final Go decision",
            "FIN-218 retains final release-owner sign-off"
        })
        {
            Assert.Contains(phrase, guide, StringComparison.Ordinal);
        }
    }

    private static string Rooted(string path)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
                {
                    return Path.Combine(directory.FullName, path.Replace('/', Path.DirectorySeparatorChar));
                }
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
