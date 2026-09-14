using System.Text.Json;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class OperationalReleaseReadinessTests
{
    private const string Guide = "docs/engineering/operational-release-readiness.md";

    [Fact]
    public void BlankRecord_HasNoApprovalCandidateOrSpendingAuthority()
    {
        using var document = JsonDocument.Parse(Read("docs/delivery/operational-readiness-record.json"));
        var root = document.RootElement;
        Assert.Equal(new[]
        {
            "classification", "candidateCommit", "artifactDigest", "environment", "capabilityScope",
            "releaseOwner", "backupOwner", "verifiedAtUtc", "decision", "runtimeAcceptance",
            "additionalSpendAuthorized", "checks"
        }, root.EnumerateObject().Select(property => property.Name));
        Assert.Equal("blank-operational-release-record", root.GetProperty("classification").GetString());
        foreach (var name in new[] { "candidateCommit", "artifactDigest", "environment", "capabilityScope", "releaseOwner", "backupOwner", "verifiedAtUtc" })
        {
            Assert.Equal(JsonValueKind.Null, root.GetProperty(name).ValueKind);
        }

        Assert.Equal("Blocked", root.GetProperty("decision").GetString());
        Assert.False(root.GetProperty("runtimeAcceptance").GetBoolean());
        Assert.False(root.GetProperty("additionalSpendAuthorized").GetBoolean());
    }

    [Fact]
    public void RequiredChecks_CoverEveryJiraAreaAndStartWithVisibleBlockers()
    {
        using var document = JsonDocument.Parse(Read("docs/delivery/operational-readiness-record.json"));
        var checks = document.RootElement.GetProperty("checks").EnumerateArray().ToArray();
        Assert.Equal(new[] { "health", "logs", "alerts", "audit", "admin", "support", "mcp" },
            checks.Select(check => check.GetProperty("area").GetString()));
        Assert.Equal(Enumerable.Range(1, 7).Select(number => $"OPS-READY-{number:000}"),
            checks.Select(check => check.GetProperty("id").GetString()));
        foreach (var check in checks)
        {
            Assert.Equal(new[] { "id", "area", "ownerRole", "required", "status", "evidence", "blocker" },
                check.EnumerateObject().Select(property => property.Name));
            Assert.True(check.GetProperty("required").GetBoolean());
            Assert.Equal("Blocked", check.GetProperty("status").GetString());
            Assert.Empty(check.GetProperty("evidence").EnumerateArray());
            Assert.Matches("^[a-z][a-z-]+$", check.GetProperty("ownerRole").GetString()!);
            Assert.False(string.IsNullOrWhiteSpace(check.GetProperty("blocker").GetString()));
            Assert.Contains(check.GetProperty("id").GetString()!, Read(Guide), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Guide_DistinguishesDefinitionAndScopeFromRuntimeGoDecision()
    {
        var guide = System.Text.RegularExpressions.Regex.Replace(Read(Guide), @"\s+", " ");
        foreach (var phrase in new[]
        {
            "All seven required checks", "exact candidate", "artifact digest", "Not applicable",
            "cannot waive them", "No-Go", "do not require or claim them as enabled implicitly",
            "remain unregistered", "health lacks a freshness timestamp", "return zeros",
            "approved, truthful unavailable behavior", "No production mutation",
            "default additional spend is zero", "rather than auto-closing", "FIN-205", "FIN-215", "FIN-216", "FIN-218"
        })
        {
            Assert.Contains(phrase, guide, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Guide_IsDiscoverableFromOperationalEntryPoints()
    {
        foreach (var path in new[]
        {
            "docs/README.md", "docs/engineering/observability-admin-test-plan.md",
            "docs/engineering/alerting-and-incident-priorities.md", "docs/engineering/mcp-operational-diagnostics.md"
        })
        {
            Assert.Contains("operational-release-readiness.md", Read(path), StringComparison.Ordinal);
        }
    }

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
