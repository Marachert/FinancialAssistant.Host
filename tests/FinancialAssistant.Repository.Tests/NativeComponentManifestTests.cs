using System.Diagnostics;
using System.Text.Json.Nodes;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class NativeComponentManifestTests
{
    [Fact]
    public async Task Inventory_CoversProductionHosts_WithoutApprovingRelease()
    {
        var result = await ValidateAsync(ReadManifest());
        Assert.Equal(0, result.ExitCode);
        var report = JsonNode.Parse(result.Output)!;
        Assert.True(report["schemaValid"]!.GetValue<bool>());
        Assert.False(report["releaseReady"]!.GetValue<bool>());
        Assert.Equal(15, report["hosts"]!.GetValue<int>());
        Assert.Equal(3, report["targets"]!.GetValue<int>());
        Assert.NotEmpty(report["blockers"]!.AsArray());
    }

    [Theory]
    [InlineData("release")]
    [InlineData("spending")]
    [InlineData("retain-data")]
    [InlineData("duplicate-port")]
    [InlineData("missing-host")]
    [InlineData("unknown-dependency")]
    [InlineData("dependency-cycle")]
    [InlineData("missing-hash")]
    [InlineData("invalid-hash")]
    [InlineData("http-source")]
    [InlineData("escaping-project")]
    [InlineData("public-bind")]
    [InlineData("unsupported-target")]
    public async Task UnsafeOrIncompleteInventory_IsRejected(string mutation)
    {
        var manifest = ReadManifest();
        var hosts = manifest["hosts"]!.AsArray();
        var packages = manifest["packages"]!.AsArray();
        switch (mutation)
        {
            case "release": manifest["releaseReady"] = true; break;
            case "spending": manifest["policy"]!["automaticSpendingAllowed"] = true; break;
            case "retain-data": manifest["policy"]!["uninstallRetainsData"] = false; break;
            case "duplicate-port": hosts[1]!["port"] = hosts[0]!["port"]!.DeepClone(); break;
            case "missing-host": hosts.RemoveAt(hosts.Count - 1); break;
            case "unknown-dependency": hosts[0]!["dependsOn"]!.AsArray().Add("missing"); break;
            case "dependency-cycle": hosts[1]!["dependsOn"]!.AsArray().Add("gateway"); break;
            case "missing-hash": packages[0]!["hash"] = null; break;
            case "invalid-hash": packages[0]!["hash"] = "1234"; break;
            case "http-source": packages[0]!["origin"] = "http://example.invalid/payload.exe"; break;
            case "escaping-project": hosts[0]!["project"] = "../outside.csproj"; break;
            case "public-bind": hosts[0]!["bindAddress"] = "0.0.0.0"; break;
            case "unsupported-target": manifest["targets"]![0]!["architecture"] = "arm64"; break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        var result = await ValidateAsync(manifest);
        Assert.NotEqual(0, result.ExitCode);
    }

    private static JsonNode ReadManifest() => JsonNode.Parse(File.ReadAllText(
        Path.Combine(Root(), "infra/windows-native/component-manifest.json")))!;

    private static async Task<(int ExitCode, string Output)> ValidateAsync(JsonNode manifest)
    {
        var temporaryFile = Path.Combine(Path.GetTempPath(), $"fa-manifest-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(temporaryFile, manifest.ToJsonString());
            var start = new ProcessStartInfo("pwsh")
            {
                WorkingDirectory = Root(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File",
                "tools/scripts/test-native-component-manifest.ps1", "-ManifestPath", temporaryFile })
            {
                start.ArgumentList.Add(argument);
            }

            using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                throw;
            }

            return (process.ExitCode, await stdout + await stderr);
        }
        finally
        {
            File.Delete(temporaryFile);
        }
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
