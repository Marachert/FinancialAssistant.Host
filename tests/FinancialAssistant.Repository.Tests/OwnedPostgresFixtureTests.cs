using System.Diagnostics;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class OwnedPostgresFixtureTests
{
    [Theory]
    [InlineData("backend/shared/persistence/FinancialAssistant.Shared.Persistence.Integration/bin/Release/net8.0/FinancialAssistant.Shared.Persistence.Integration.dll", "FAIL PostgreSQL integration at fixture validation")]
    [InlineData("backend/services/identity/FinancialAssistant.Identity.Persistence.Integration/bin/Release/net8.0/FinancialAssistant.Identity.Persistence.Integration.dll", "FAIL identity PostgreSQL at owned fixture validation")]
    public async Task MissingFixtureConfigurationFailsWithoutDatabaseMutation(string executable, string expectedFailure)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Root(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(Root(), executable));
        foreach (var name in new[] { "FA_PERSISTENCE_TEST_PASSWORD", "FA_PERSISTENCE_TEST_PORT", "FA_PERSISTENCE_TEST_DIRECTORY", "FA_PERSISTENCE_TEST_MODE" })
        {
            start.Environment.Remove(name);
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Integration process unavailable.");
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
        Assert.Equal(1, process.ExitCode);
        Assert.Contains(expectedFailure, await output + await error);
    }

    [Fact]
    public async Task UnverifiedArchiveFailsBeforeProcessStartup()
    {
        var root = Root();
        var directory = Path.Combine(root, ".codex-runtime", "fixture-negative-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var archive = Path.Combine(directory, "synthetic-untrusted.zip");
        try
        {
            await File.WriteAllTextAsync(archive, "synthetic unverified archive");
            var start = new ProcessStartInfo("pwsh")
            {
                WorkingDirectory = root,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", "tools/scripts/test-owned-postgres.ps1", "-ArchivePath", archive })
            {
                start.ArgumentList.Add(argument);
            }
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
            var combined = await output + await error;
            Assert.NotEqual(0, process.ExitCode);
            Assert.Contains(OperatingSystem.IsWindows() ? "archive digest mismatch" : "Windows PostgreSQL archive", combined);
            Assert.DoesNotContain("PASS initial PostgreSQL", combined);
            Assert.Equal("synthetic unverified archive", await File.ReadAllTextAsync(archive));
        }
        finally { File.Delete(archive); Directory.Delete(directory, recursive: false); }
    }

    [Fact]
    public void FixtureHasExplicitOwnershipCleanupAndNoInstallation()
    {
        var script = File.ReadAllText(Path.Combine(Root(), "tools/scripts/test-owned-postgres.ps1"));
        Assert.Contains("Get-FileHash", script);
        Assert.Contains("$serverProcess.StartTime", script);
        Assert.Contains("$serverProcess.Path", script);
        Assert.Contains("$saved[$name]", script);
        Assert.Contains("releaseReady=$false", script);
        Assert.Contains("--auth-host=scram-sha-256", script);
        Assert.Contains("[ValidateSet('Foundation','Identity')]", script);
        Assert.DoesNotContain("New-Service", script);
        Assert.DoesNotContain("sc.exe", script);
        Assert.DoesNotContain("Unblock-File", script);
        Assert.DoesNotContain("Remove-Item -Recurse", script);
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
