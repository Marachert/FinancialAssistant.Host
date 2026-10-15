using System.Diagnostics;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class NativeAdapterFixtureTests
{
    [Theory]
    [InlineData("child")]
    [InlineData("check-reparse")]
    public async Task UnownedExistingDirectoryIsRejectedBeforeMutation(string mode)
    {
        var root = Root();
        var path = Path.Combine(root, ".codex-runtime", "native-negative-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        var canary = Path.Combine(path, "canary");
        try
        {
            await File.WriteAllTextAsync(canary, "synthetic-owned-canary");
            var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(root, "backend/shared/native-files/FinancialAssistant.NativeAdapters.Integration/bin/Release/net8.0/FinancialAssistant.NativeAdapters.Integration.dll"));
            start.ArgumentList.Add(mode); start.ArgumentList.Add(path); start.ArgumentList.Add(Guid.NewGuid().ToString("N"));
            using var process = Process.Start(start) ?? throw new IOException("Fixture process unavailable.");
            var started = process.StartTime;
            var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await process.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited && process.StartTime == started) { process.Kill(entireProcessTree: false); await process.WaitForExitAsync(); }
                throw;
            }
            Assert.Equal(1, process.ExitCode);
            var combined = await output + await error;
            Assert.Contains("Native synthetic adapter fixture failed", combined);
            Assert.DoesNotContain("fixture passed", combined);
            Assert.Equal("synthetic-owned-canary", await File.ReadAllTextAsync(canary));
            Assert.Single(Directory.GetFileSystemEntries(path));
        }
        finally { File.Delete(canary); Directory.Delete(path, recursive: false); }
    }

    [Fact]
    public void CrashFixtureChecksOwnershipAndCannotInstallOrKillProcessTrees()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "backend/shared/native-files/FinancialAssistant.NativeAdapters.Integration/Program.cs"));
        Assert.Contains("process.StartTime.ToUniversalTime() != started", source);
        Assert.Contains("process.MainModule!.FileName", source);
        Assert.Contains("process.Kill(entireProcessTree: false)", source);
        Assert.Contains("CreateNoWindow = true", source);
        Assert.Contains("ValidateRoot(root, run)", source);
        Assert.Contains("10 * 1024 * 1024", source);
        Assert.DoesNotContain("entireProcessTree: true", source);
        Assert.DoesNotContain("New-Service", source);
        Assert.DoesNotContain("sc.exe", source);
    }

    [Fact]
    public void JunctionFixtureVerifiesExactTemporaryTargetBeforeCleanup()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "tools/scripts/test-native-adapters.ps1"));
        Assert.Contains("if (-not $IsWindows)", source);
        Assert.Contains("Refusing to reuse", source);
        Assert.Contains("if ($owned)", source);
        Assert.Contains("GetDirectoryName($root)", source);
        Assert.Contains("ReparsePoint", source);
        Assert.Contains("Remove-Item -LiteralPath $junction -Force", source);
        Assert.Contains("Remove-Item -LiteralPath $root -Recurse -Force", source);
        Assert.DoesNotContain("Remove-Item -Path", source);
        Assert.DoesNotContain("New-Service", source);
        Assert.DoesNotContain("Unblock-File", source);
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        { if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln"))) { return directory.FullName; } }
        throw new DirectoryNotFoundException("Repository root unavailable.");
    }
}
