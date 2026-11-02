using System.Diagnostics;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class NativePersistenceQualificationTests
{
    [Fact]
    public void MatrixRequiresEveryOwnerAndStructuredFreshCleanupEvidence()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "tools/scripts/test-native-persistence.ps1"));
        Assert.Contains("@('Foundation','Identity','ProfileCategory','FinancialRecords','Intake','ReceiptAi','Projections','Insights','Audit')", source);
        Assert.Contains("status='not-run'", source);
        Assert.Contains("$entry.status = 'failed'", source);
        Assert.Contains("Scenario evidence missing; console success is insufficient.", source);
        Assert.Contains("$evidence.cleanupSucceeded -cne $true", source);
        Assert.Contains("$evidence.verifiedAt -lt $scenarioStarted", source);
        Assert.Contains("$evidence.interruptedTransactionRecovered -cne $true", source);
        Assert.Contains("data/postmaster.pid", source);
        Assert.Contains("fixture-password.txt", source);
        Assert.Contains("Incomplete persistence matrix is not a pass.", source);
        Assert.Contains("'build','FinancialAssistant.Backend.sln'", source);
        Assert.Contains("WaitForExit(1200000)", source);
        Assert.Contains("SetAccessRuleProtection($true, $false)", source);
        Assert.Contains("[IO.FileMode]::CreateNew", source);
        Assert.Contains("installed=$false;releaseReady=$false", source);
        Assert.DoesNotContain("Invoke-WebRequest", source);
        Assert.DoesNotContain("Start-Service", source);
        Assert.DoesNotContain("Unblock-File", source);
    }

    [Fact]
    public void FoundationCrashesDuringAnActualUncommittedDomainAndOutboxTransaction()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "backend/shared/persistence/FinancialAssistant.Shared.Persistence.Integration/Program.cs"));
        Assert.Contains("mode == \"interrupt\"", source);
        Assert.Contains("tx.InsertAsync(\"account\", \"synthetic_owner\", \"interrupted\"", source);
        Assert.Contains("tx.InsertAsync(\"outbox\", \"synthetic_owner\", \"interrupted\"", source);
        Assert.Contains("\"interrupted-ready\"), FileMode.CreateNew", source);
        Assert.Contains("tx.GetAsync(\"account\", \"synthetic_owner\", \"interrupted\")) is null", source);
        Assert.Contains("tx.GetAsync(\"outbox\", \"synthetic_owner\", \"interrupted\")) is null", source);
        var runner = File.ReadAllText(Path.Combine(Root(), "tools/scripts/test-owned-postgres.ps1"));
        Assert.Contains("Live interrupted transaction did not become ready before its deadline.", runner);
        Assert.Contains("'-m','immediate','-w','stop'", runner);
        Assert.Contains("Interrupted probe cleanup failed; PostgreSQL cleanup was still attempted.", runner);
        Assert.Contains("Write-NewJson $ResultFile $result", runner);
        Assert.Contains("$serverPid -le 0", runner);
        Assert.Contains("Cannot confirm failed bootstrap process cleanup.", runner);
        Assert.DoesNotContain("& dotnet run", runner);
    }

    [Theory]
    [InlineData("test-native-persistence.ps1", "PostgreSQL archive digest mismatch.")]
    [InlineData("test-owned-postgres.ps1", "Invalid owned qualification result target.")]
    public async Task InvalidInputsFailBeforeCreatingFixtureOrOverwritingEvidence(string script, string expected)
    {
        var directory = Path.Combine(Root(), ".codex-runtime", "qualification-negative-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var archive = Path.Combine(directory, "untrusted.zip");
        var evidence = Path.Combine(directory, "foundation.json");
        try
        {
            await File.WriteAllTextAsync(archive, "synthetic untrusted package");
            await File.WriteAllTextAsync(evidence, "preserved synthetic evidence");
            var start = new ProcessStartInfo("pwsh")
            {
                WorkingDirectory = Root(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", "tools/scripts/" + script, "-ArchivePath", archive })
            { start.ArgumentList.Add(argument); }
            if (script == "test-owned-postgres.ps1")
            { start.ArgumentList.Add("-ResultFile"); start.ArgumentList.Add(evidence); }
            using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell unavailable.");
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                throw;
            }
            Assert.NotEqual(0, process.ExitCode);
            var combined = await output + await error;
            Assert.Contains(OperatingSystem.IsWindows() ? expected : script == "test-native-persistence.ps1"
                ? "qualification requires Windows" : "Windows PostgreSQL archive", combined);
            Assert.Equal("preserved synthetic evidence", await File.ReadAllTextAsync(evidence));
            Assert.Equal(2, Directory.GetFiles(directory).Length);
            Assert.Empty(Directory.GetDirectories(directory));
        }
        finally { File.Delete(archive); File.Delete(evidence); Directory.Delete(directory, recursive: false); }
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln"))) { return directory.FullName; }
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
