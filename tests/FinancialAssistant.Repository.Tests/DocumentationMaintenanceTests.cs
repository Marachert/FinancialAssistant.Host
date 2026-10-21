using System.Text.RegularExpressions;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class DocumentationMaintenanceTests
{
    [Fact]
    public void CurrentProgressSummariesUseTheSameCensus()
    {
        var progress = Read("docs/agent/POC_PROGRESS.md");
        var snapshot = Regex.Match(progress, @"\bP9 (\d+/\d+ \(\d+(?:\.\d+)?%\))");
        var delivery = Regex.Match(progress, @"^- FIN-38 P9 progress is (\d+/\d+ \(\d+(?:\.\d+)?%\));", RegexOptions.Multiline);
        Assert.True(snapshot.Success && delivery.Success);
        Assert.Equal(snapshot.Groups[1].Value, delivery.Groups[1].Value);
        var epic = Regex.Match(progress, @"^\| FIN-38 \| P9[^\r\n]*\| (\d+) \| (\d+) \| (\d+(?:\.\d+)?%) \|", RegexOptions.Multiline);
        Assert.True(epic.Success);
        Assert.Equal(snapshot.Groups[1].Value, $"{epic.Groups[1].Value}/{epic.Groups[2].Value} ({epic.Groups[3].Value})");
        var canonical = Regex.Match(progress, @"\*\*(\d+) of (\d+) canonical POC leaf tickets are Done\*\*");
        var percentage = Regex.Match(progress, @"POC backlog completion[^\r\n]*is \*\*(\d+(?:\.\d+)?%)\*\*");
        var total = Regex.Match(progress, @"^\| \*\*Total\*\* \| \*\*Canonical POC leaf scope\*\* \| \*\*(\d+)\*\* \| \*\*(\d+)\*\* \| \*\*(\d+(?:\.\d+)?%)\*\* \|", RegexOptions.Multiline);
        Assert.True(canonical.Success && percentage.Success && total.Success);
        Assert.Equal(canonical.Groups[1].Value, total.Groups[1].Value);
        Assert.Equal(canonical.Groups[2].Value, total.Groups[2].Value);
        Assert.Equal(percentage.Groups[1].Value, total.Groups[3].Value);
        var parent = Regex.Match(progress, @"In Progress\s*(\d+/\d+); P9");
        var parentSummary = Regex.Match(progress, @"^- FIN-270 is the priority parent with (\d+/\d+) children Done;", RegexOptions.Multiline);
        Assert.True(parent.Success && parentSummary.Success);
        Assert.Equal(parent.Groups[1].Value, parentSummary.Groups[1].Value);
    }

    [Fact]
    public void SummaryReadmeSeparatesMergedStorageFromPendingIntegration()
    {
        var summary = Read("backend/services/financial-summary/README.md");
        Assert.Contains("FIN-287 is Done", summary, StringComparison.Ordinal);
        Assert.Contains("FIN-276", summary, StringComparison.Ordinal);
        Assert.Contains("FIN-286", summary, StringComparison.Ordinal);
        Assert.Contains("FIN-209", summary, StringComparison.Ordinal);
        Assert.DoesNotContain("complete FIN-287 delivery gate are still in progress", summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("docs/architecture/current-implementation.md")]
    [InlineData("docs/architecture/storage-policy.md")]
    [InlineData("docs/agent/DOCUMENTATION_MAINTENANCE.md")]
    [InlineData("docs/reviews/documentation-audit-20260910.md")]
    public void DocumentationEntryPoints_ArePresentAndIndexed(string path)
    {
        Assert.False(string.IsNullOrWhiteSpace(Read(path)));
        Assert.Contains(path["docs/".Length..], Read("docs/README.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void StorageAndReadiness_DoNotEquateTargetWithDeployment()
    {
        var policy = Read("docs/architecture/storage-policy.md");
        Assert.Contains("PostgreSQL is the preferred target", policy, StringComparison.Ordinal);
        Assert.Contains("not a claim", policy, StringComparison.Ordinal);
        Assert.Contains("in-memory", policy, StringComparison.Ordinal);
        Assert.Contains("No data migration", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("| Operational storage | Elasticsearch-first", Read("README.md"), StringComparison.Ordinal);
        Assert.Contains("First-user testing remains Not Ready", Read("docs/architecture/current-implementation.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void AgentInstructions_RequireSynchronizedDocsAndVerifiedClosure()
    {
        var maintenance = Read("docs/agent/DOCUMENTATION_MAINTENANCE.md");
        Assert.Contains("merged=true", maintenance, StringComparison.Ordinal);
        Assert.Contains("actual merge SHA/time", maintenance, StringComparison.Ordinal);
        Assert.Contains("POC_PROGRESS.md", maintenance, StringComparison.Ordinal);
        Assert.Contains("truncated", maintenance, StringComparison.Ordinal);
        Assert.Contains("DOCUMENTATION_MAINTENANCE.md", Read("AGENTS.md"), StringComparison.Ordinal);
        Assert.Contains("without separate explicit approval", Read("docs/agent/SECURITY_AND_BLOCKERS.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void ApiIndex_ExposesExistingOperationalContracts()
    {
        var index = Read("docs/api/README.md");
        foreach (var name in new[] { "notification-preferences-v1.md", "monitoring-admin-v1.md", "audit-admin-v1.md", "mcp-server-v1.md" })
        {
            Assert.Contains($"]({name})", index, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(Read("docs/api/" + name)));
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
