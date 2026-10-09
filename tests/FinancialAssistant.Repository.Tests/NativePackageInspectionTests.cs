using System.Buffers.Binary;
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
    [InlineData("zip64")]
    [InlineData("comment")]
    public async Task BoundedZipMetadata_AcceptsZip64AndComments(string mutation)
    {
        var result = await InspectAsync(mutation, "SHA256");
        Assert.Equal(0, result.ExitCode);
        var report = JsonNode.Parse(result.Output)!;
        Assert.True(report["digestVerified"]!.GetValue<bool>());
        Assert.Equal(2, report["archiveFiles"]!.GetValue<int>());
        Assert.False(report["releaseReady"]!.GetValue<bool>());
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
    [InlineData("com-superscript-1", "Unsafe archive path")]
    [InlineData("com-superscript-2", "Unsafe archive path")]
    [InlineData("com-superscript-3", "Unsafe archive path")]
    [InlineData("lpt-superscript-1", "Unsafe archive path")]
    [InlineData("lpt-superscript-2", "Unsafe archive path")]
    [InlineData("lpt-superscript-3", "Unsafe archive path")]
    [InlineData("preflight-entry-limit", "Archive entry limit exceeded")]
    [InlineData("forged-entry-count", "Archive entry limit exceeded")]
    [InlineData("directory-size-limit", "Archive central-directory byte limit exceeded")]
    [InlineData("count-mismatch", "ZIP directory entry count mismatch")]
    [InlineData("directory-bounds", "Invalid ZIP directory bounds")]
    [InlineData("directory-truncated", "Invalid ZIP central directory")]
    [InlineData("multidisk", "Multi-disk ZIP archives are not supported")]
    [InlineData("zip64-entry-limit", "Archive entry limit exceeded")]
    [InlineData("zip64-offset", "Invalid ZIP64 offset")]
    [InlineData("zip64-count", "ZIP directory entry count mismatch")]
    [InlineData("eocd-missing", "Invalid ZIP archive")]
    [InlineData("comment-mismatch", "Invalid ZIP archive")]
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
                        "com-superscript-1" => "package/COM\u00B9.txt",
                        "com-superscript-2" => "package/COM\u00B2.txt",
                        "com-superscript-3" => "package/COM\u00B3.txt",
                        "lpt-superscript-1" => "package/LPT\u00B9.txt",
                        "lpt-superscript-2" => "package/LPT\u00B2.txt",
                        "lpt-superscript-3" => "package/LPT\u00B3.txt",
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
            if (mutation != "broken")
            {
                var end = payload.Length - 22;
                var directory = (int)BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(end + 16));
                switch (mutation)
                {
                    case "preflight-entry-limit":
                        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(end + 8), 12);
                        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(end + 10), 12);
                        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(directory), 0);
                        break;
                    case "count-mismatch":
                    case "forged-entry-count":
                        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(end + 8), 1);
                        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(end + 10), 1);
                        break;
                    case "directory-bounds": BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(end + 16), (uint)payload.Length); break;
                    case "directory-truncated":
                        var size = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(end + 12));
                        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(end + 12), size - 1);
                        break;
                    case "multidisk": BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(end + 4), 1); break;
                    case "eocd-missing": BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(end), 0); break;
                    case "comment-mismatch": BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(end + 20), 1); break;
                    case "comment":
                        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(end + 20), 8);
                        payload = payload.Concat("comment!"u8.ToArray()).ToArray();
                        break;
                    case "zip64": payload = AddZip64Trailer(payload, 2); break;
                    case "zip64-count": payload = AddZip64Trailer(payload, 1); break;
                    case "zip64-entry-limit":
                        payload = AddZip64Trailer(payload, ulong.MaxValue);
                        BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(directory), 0);
                        break;
                    case "zip64-offset":
                        payload = AddZip64Trailer(payload, 2);
                        BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(payload.Length - 22 - 20 + 8), ulong.MaxValue);
                        break;
                }
                await File.WriteAllBytesAsync(payloadPath, payload);
            }
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
            if (mutation is "entry-limit" or "preflight-entry-limit" or "forged-entry-count") { start.ArgumentList.Add("-MaxArchiveEntries"); start.ArgumentList.Add("1"); }
            if (mutation == "directory-size-limit") { start.ArgumentList.Add("-MaxCentralDirectoryBytes"); start.ArgumentList.Add("1"); }
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

    private static byte[] AddZip64Trailer(byte[] payload, ulong count)
    {
        var end = payload.Length - 22;
        var directorySize = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(end + 12));
        var directoryOffset = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(end + 16));
        var trailer = payload.AsSpan(end).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(trailer.AsSpan(8), ushort.MaxValue);
        BinaryPrimitives.WriteUInt16LittleEndian(trailer.AsSpan(10), ushort.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(trailer.AsSpan(12), uint.MaxValue);
        BinaryPrimitives.WriteUInt32LittleEndian(trailer.AsSpan(16), uint.MaxValue);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(payload, 0, end);
        var zip64Offset = (ulong)stream.Position;
        writer.Write(0x06064B50u);
        writer.Write(44ul);
        writer.Write((ushort)45);
        writer.Write((ushort)45);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(count);
        writer.Write(count);
        writer.Write((ulong)directorySize);
        writer.Write((ulong)directoryOffset);
        writer.Write(0x07064B50u);
        writer.Write(0u);
        writer.Write(zip64Offset);
        writer.Write(1u);
        writer.Write(trailer);
        writer.Flush();
        return stream.ToArray();
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
