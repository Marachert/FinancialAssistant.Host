using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Xunit;

namespace FinancialAssistant.Repository.Tests;

public sealed class NativePackageInspectionTests
{
    [Theory]
    [InlineData("SHA256")]
    [InlineData("SHA512")]
    public async Task MatchingDigest_ReportsArchiveMetadataWithoutApprovingRelease(string algorithm)
    {
        var result = await InspectAsync("valid", algorithm);
        Assert.Equal(0, result.ExitCode);
        var report = JsonNode.Parse(result.Output)!;
        Assert.True(report["digestVerified"]!.GetValue<bool>());
        Assert.Equal(2, report["archiveFiles"]!.GetValue<int>());
        Assert.Equal(18, report["declaredExpandedBytes"]!.GetValue<long>());
        Assert.Equal("package/LICENSE.txt", report["noticeCandidates"]![0]!.GetValue<string>());
        Assert.False(report["releaseReady"]!.GetValue<bool>());
        Assert.False(report["binarySignaturesVerified"]!.GetValue<bool>());
        Assert.False(report["licenseReviewComplete"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("digest", "Payload digest does not match")]
    [InlineData("missing-digest", "Publisher digest is required")]
    [InlineData("unknown-package", "Package ID must identify")]
    [InlineData("traversal", "Unsafe archive path")]
    [InlineData("absolute", "Unsafe archive path")]
    [InlineData("drive", "Unsafe archive path")]
    [InlineData("reserved", "Unsafe archive path")]
    [InlineData("trailing-dot", "Unsafe archive path")]
    [InlineData("file-parent", "Archive file is also a parent directory")]
    [InlineData("duplicate", "Duplicate Windows archive path")]
    [InlineData("symlink", "Archive symbolic links")]
    [InlineData("empty", "Archive has no payload files")]
    [InlineData("entry-limit", "Archive entry limit exceeded")]
    [InlineData("byte-limit", "Archive expanded-byte limit exceeded")]
    [InlineData("broken", "Invalid ZIP archive")]
    public async Task UntrustedOrUnsafePayload_IsRejected(string mutation, string message)
    {
        var result = await InspectAsync(mutation, "SHA256");
        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(message, result.Output);
    }

    private static async Task<(int ExitCode, string Output)> InspectAsync(string mutation, string algorithm)
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"fa-inspection-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var payloadPath = Path.Combine(temporaryDirectory, "synthetic.zip");
            using (var archive = ZipFile.Open(payloadPath, ZipArchiveMode.Create))
            {
                if (mutation != "empty")
                {
                    var name = mutation switch
                    {
                        "traversal" => "../outside.txt",
                        "absolute" => "/outside.txt",
                        "drive" => "C:/outside.txt",
                        "reserved" => "package/CON.txt",
                        "trailing-dot" => "package/content.",
                        _ => "package/content.txt"
                    };
                    var entry = archive.CreateEntry(name);
                    if (mutation == "symlink") { entry.ExternalAttributes = 0xA1FF << 16; }
                    using (var stream = entry.Open()) { stream.Write("content"u8); }
                    using (var stream = archive.CreateEntry("package/LICENSE.txt").Open()) { stream.Write("test notice"u8); }
                    if (mutation == "duplicate") { archive.CreateEntry("PACKAGE/content.txt"); }
                    if (mutation == "file-parent") { archive.CreateEntry("package/content.txt/child.txt"); }
                }
            }

            if (mutation == "broken") { await File.WriteAllTextAsync(payloadPath, "invalid zip"); }
            var payload = await File.ReadAllBytesAsync(payloadPath);
            var hash = Convert.ToHexString(algorithm == "SHA256" ? SHA256.HashData(payload) : SHA512.HashData(payload)).ToLowerInvariant();
            var manifest = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(Root(), "infra/windows-native/component-manifest.json")))!;
            var package = manifest["packages"]!.AsArray().Single(entry => entry!["id"]!.GetValue<string>() == "postgresql")!;
            package["hashAlgorithm"] = mutation == "missing-digest" ? null : algorithm;
            package["hash"] = mutation == "missing-digest" ? null : mutation == "digest" ? new string('0', 64) : hash;
            var manifestPath = Path.Combine(temporaryDirectory, "manifest.json");
            await File.WriteAllTextAsync(manifestPath, manifest.ToJsonString());
            var start = new ProcessStartInfo("pwsh")
            {
                WorkingDirectory = Root(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in new[] { "-NoProfile", "-NonInteractive", "-File",
                "tools/scripts/inspect-native-package.ps1", "-PackageId", mutation == "unknown-package" ? "missing" : "postgresql",
                "-PayloadPath", payloadPath, "-ManifestPath", manifestPath }) { start.ArgumentList.Add(argument); }
            if (mutation == "entry-limit") { start.ArgumentList.Add("-MaxArchiveEntries"); start.ArgumentList.Add("1"); }
            if (mutation == "byte-limit") { start.ArgumentList.Add("-MaxExpandedBytes"); start.ArgumentList.Add("1"); }
            using var process = Process.Start(start) ?? throw new InvalidOperationException("PowerShell did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
                throw;
            }
            return (process.ExitCode, await stdout + await stderr);
        }
        finally { Directory.Delete(temporaryDirectory, recursive: true); }
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
