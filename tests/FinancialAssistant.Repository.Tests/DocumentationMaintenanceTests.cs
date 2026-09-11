using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class DocumentationMaintenanceTests
{
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
