using System.Diagnostics;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class NativeHostPublishingTests
{
    [Theory]
    [InlineData("../outside", "all")]
    [InlineData("con", "all")]
    [InlineData("com1", "all")]
    [InlineData("valid", "unknown-service")]
    public async Task InvalidPublishRequest_FailsBeforeCreatingCandidate(string candidate, string service)
    {
        var result = await RunAsync(candidate, service);
        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("dotnet publish", result.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unqualified-native-host-candidate", result.Output);
    }

    [Fact]
    public async Task ExistingCandidate_IsNotOverwrittenOrDeleted()
    {
        var candidate = $"test-{Guid.NewGuid():N}";
        var directory = Path.Combine(Root(), ".codex-runtime", "native-hosts", candidate);
        Directory.CreateDirectory(directory);
        var sentinel = Path.Combine(directory, "synthetic-owner.txt");
        try
        {
            await File.WriteAllTextAsync(sentinel, "synthetic-existing-owner");
            var result = await RunAsync(candidate, "gateway");
            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("Candidate output already exists", result.Output);
            Assert.Equal("synthetic-existing-owner", await File.ReadAllTextAsync(sentinel));
            Assert.False(File.Exists(Path.Combine(directory, "candidate.json")));
        }
        finally
        {
            File.Delete(sentinel);
            Directory.Delete(directory, recursive: false);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string candidate, string service)
    {
        var start = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = Root(),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File", "tools/scripts/publish-native-hosts.ps1",
            "-CandidateId", candidate, "-ServiceId", service }) { start.ArgumentList.Add(argument); }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw;
        }

        return (process.ExitCode, await stdout + await stderr);
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
