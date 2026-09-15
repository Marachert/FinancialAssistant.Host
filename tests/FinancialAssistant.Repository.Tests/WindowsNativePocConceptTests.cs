using System.Text.RegularExpressions;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class WindowsNativePocConceptTests
{
    [Fact]
    public void Decision_StatesNativeTargetAndUnverifiedRuntimeBoundary()
    {
        var decision = Regex.Replace(Read("docs/architecture/windows-native-poc.md"), @"\s+", " ");
        foreach (var requirement in new[]
        {
            "Windows 11", "Windows Server 2022", "Windows Server 2025",
            "Desktop Experience", "no Docker, WSL or Linux VM requirement",
            "Approved target, not implemented or runtime accepted",
            "retains data and backups by default", "First-user testing remains",
            "No SDK, Git checkout", "FIN-270", "FIN-281"
        })
        {
            Assert.Contains(requirement, decision, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("infra/windows-poc/README.md")]
    [InlineData("docs/delivery/windows-server-poc-deployment.md")]
    public void LegacyRunbooks_ExplicitlySupersedeThePocTarget(string path)
    {
        var guide = Read(path);
        Assert.Contains("Superseded POC Target", guide, StringComparison.Ordinal);
        Assert.Contains("windows-native-poc.md", guide, StringComparison.Ordinal);
        Assert.Contains("FIN-270", guide, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("infra/README.md")]
    [InlineData("infra/docker-compose/README.md")]
    public void InfrastructureIndexes_PointToTheNativeTarget(string path)
    {
        var guide = Read(path);
        Assert.Contains("windows-native-poc.md", guide, StringComparison.Ordinal);
        Assert.Contains("historical", guide, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("production-like", guide, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string path)
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
                {
                    return File.ReadAllText(Path.Combine(directory.FullName, path));
                }
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
