using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class CodexDeliveryBootstrapTests
{
    [Fact]
    public void BootstrapFiles_ExistAndContainCriticalSafetyRules()
    {
        var repositoryRoot = FindRepositoryRoot();

        var requiredFiles = new[]
        {
            "AGENTS.md",
            "docs/agent/PROJECT_INSTRUCTIONS.md",
            "docs/agent/DELIVERY_WORKFLOW.md",
            "docs/agent/SECURITY_AND_BLOCKERS.md",
            "docs/agent/DESKTOP_CODEX_SETUP.md",
            ".agents/skills/financial-assistant-delivery/SKILL.md",
            ".codex/config.toml",
            ".codex/rules/delivery.rules",
            "tools/delivery/jira.ps1",
            "tools/delivery/confluence.ps1"
        };

        foreach (var path in requiredFiles)
        {
            Assert.True(
                File.Exists(ToRepositoryPath(repositoryRoot, path)),
                $"Required Codex bootstrap file '{path}' is missing.");
        }

        var agents = ReadRequiredFile(repositoryRoot, "AGENTS.md");
        var workflow = ReadRequiredFile(repositoryRoot, "docs/agent/DELIVERY_WORKFLOW.md");
        var security = ReadRequiredFile(repositoryRoot, "docs/agent/SECURITY_AND_BLOCKERS.md");
        var skill = ReadRequiredFile(
            repositoryRoot,
            ".agents/skills/financial-assistant-delivery/SKILL.md");
        var rules = ReadRequiredFile(repositoryRoot, ".codex/rules/delivery.rules");
        var gitIgnore = ReadRequiredFile(repositoryRoot, ".gitignore");

        var requiredAgentRules = new[]
        {
            "There must never be more than one active agent-owned delivery PR",
            "Never start the next Jira leaf-ticket from an unmerged branch",
            "resolve only after green CI",
            "Verify GitHub reports `merged = true`",
            "unresolved actionable review threads equal zero",
            "never push directly to `main`"
        };

        foreach (var rule in requiredAgentRules)
        {
            Assert.Contains(rule, agents, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("Before every mutation, perform a fresh read", workflow, StringComparison.Ordinal);
        Assert.Contains(".codex-runtime/delivery.lock", workflow, StringComparison.Ordinal);
        Assert.Contains("force-push", security, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CHANGES_REQUESTED", skill, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("forbidden git push origin main", rules, StringComparison.Ordinal);
        Assert.Contains("forbidden git push --force", rules, StringComparison.Ordinal);
        Assert.Contains(".codex-runtime/", gitIgnore, StringComparison.Ordinal);
    }

    [Fact]
    public void AtlassianWrappers_RequireEnvironmentCredentials()
    {
        var repositoryRoot = FindRepositoryRoot();
        var jira = ReadRequiredFile(repositoryRoot, "tools/delivery/jira.ps1");
        var confluence = ReadRequiredFile(repositoryRoot, "tools/delivery/confluence.ps1");

        foreach (var script in new[] { jira, confluence })
        {
            Assert.Contains("ATLASSIAN_SITE_URL", script, StringComparison.Ordinal);
            Assert.Contains("ATLASSIAN_EMAIL", script, StringComparison.Ordinal);
            Assert.Contains("ATLASSIAN_API_TOKEN", script, StringComparison.Ordinal);
            Assert.Contains("Required environment variable", script, StringComparison.Ordinal);
            Assert.DoesNotContain("marachert@gmail.com", script, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadRequiredFile(string repositoryRoot, string path)
    {
        var fullPath = ToRepositoryPath(repositoryRoot, path);
        Assert.True(File.Exists(fullPath), $"Required file '{path}' is missing.");
        return File.ReadAllText(fullPath);
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
