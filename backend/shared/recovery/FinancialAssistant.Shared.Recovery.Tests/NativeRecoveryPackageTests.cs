using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace FinancialAssistant.Shared.Recovery.Tests;

public sealed class NativeRecoveryPackageTests
{
    [Fact]
    public void CompletePackageCanBeWrittenToNewOwnedFileAndIndependentlyOpened()
    {
        using var fixture = new Fixture();
        var directory = Path.Combine(Root(), ".codex-runtime", "recovery-package", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "synthetic.recovery");
        try
        {
            using (var package = NativeRecoveryPackage.Create(fixture.Metadata, fixture.Captures, fixture.Passphrase))
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { package.WriteTo(output); }
            var bytes = File.ReadAllBytes(path);
            Assert.DoesNotContain("synthetic-only-financial-payload", Encoding.UTF8.GetString(bytes));
            Assert.DoesNotContain(Encoding.UTF8.GetString(fixture.Passphrase), Encoding.UTF8.GetString(bytes));
            using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            using var restored = NativeRecoveryPackage.ReadFrom(source, fixture.Passphrase);
            Assert.Equal(30, restored.Manifest.Artifacts.Count);
            foreach (var artifact in restored.Manifest.Artifacts)
            {
                var plaintext = restored.OpenArtifact(artifact.Id, fixture.Passphrase);
                try { Assert.Equal("synthetic-only-financial-payload", Encoding.UTF8.GetString(plaintext)); }
                finally { CryptographicOperations.ZeroMemory(plaintext); }
            }
            Assert.DoesNotContain("synthetic", restored.ToString());
        }
        finally { File.Delete(path); Directory.Delete(directory, recursive: false); }
    }

    [Theory]
    [InlineData("magic")]
    [InlineData("installation")]
    [InlineData("backup")]
    [InlineData("manifest-id")]
    [InlineData("wrapped-key")]
    [InlineData("manifest-length")]
    [InlineData("manifest")]
    [InlineData("artifact")]
    [InlineData("footer")]
    [InlineData("truncated-header")]
    [InlineData("truncated-artifact")]
    [InlineData("truncated-footer")]
    [InlineData("trailing-data")]
    [InlineData("wrong-unlock")]
    public void NoPackageIsReturnedForTamperingTruncationOrWrongUnlock(string mutation)
    {
        using var fixture = new Fixture();
        using var package = NativeRecoveryPackage.Create(fixture.Metadata, fixture.Captures, fixture.Passphrase);
        using var output = new MemoryStream(); package.WriteTo(output); var bytes = output.ToArray();
        switch (mutation)
        {
            case "magic": bytes[0] ^= 1; break;
            case "installation": bytes[8] ^= 1; break;
            case "backup": bytes[24] ^= 1; break;
            case "manifest-id": bytes[40] ^= 1; break;
            case "wrapped-key": bytes[100] ^= 1; break;
            case "manifest-length": BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(160, 4), int.MaxValue); break;
            case "manifest": bytes[164] ^= 1; break;
            case "artifact": bytes[164 + BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(160, 4)) + 36] ^= 1; break;
            case "footer": bytes[^1] ^= 1; break;
            case "truncated-header": bytes = bytes[..100]; break;
            case "truncated-artifact": bytes = bytes[..^100]; break;
            case "truncated-footer": bytes = bytes[..^1]; break;
            case "trailing-data": bytes = bytes.Concat(new byte[] { 0 }).ToArray(); break;
            case "wrong-unlock": fixture.Passphrase[0] ^= 1; break;
            default: throw new InvalidOperationException();
        }
        using var source = new MemoryStream(bytes, writable: false);
        var error = Assert.Throws<InvalidOperationException>(() => NativeRecoveryPackage.ReadFrom(source, fixture.Passphrase));
        Assert.Null(error.InnerException); Assert.DoesNotContain("synthetic-only", error.ToString());
    }

    [Fact]
    public void IncompleteCaptureSetAndAmbiguousPrebuiltMetadataAreRejected()
    {
        using var fixture = new Fixture();
        Assert.Throws<InvalidOperationException>(() => NativeRecoveryPackage.Create(fixture.Metadata, fixture.Captures.Take(4).ToArray(), fixture.Passphrase));
        var captures = fixture.Captures.ToArray(); captures[1] = captures[0];
        Assert.Throws<InvalidOperationException>(() => NativeRecoveryPackage.Create(fixture.Metadata, captures, fixture.Passphrase));
        fixture.Metadata.Artifacts.Add(new());
        Assert.Throws<InvalidOperationException>(() => NativeRecoveryPackage.Create(fixture.Metadata, fixture.Captures, fixture.Passphrase));
    }

    [Fact]
    public void ExistingDestinationAndWrongStreamPositionAreNeverOverwritten()
    {
        using var fixture = new Fixture();
        using var package = NativeRecoveryPackage.Create(fixture.Metadata, fixture.Captures, fixture.Passphrase);
        var existing = "synthetic-existing-owned-file"u8.ToArray();
        var original = existing.ToArray();
        using var destination = new MemoryStream(existing, writable: true);
        Assert.Throws<InvalidOperationException>(() => package.WriteTo(destination)); Assert.Equal(original, destination.ToArray());
        using var output = new MemoryStream(); package.WriteTo(output); var data = output.ToArray();
        Assert.Throws<InvalidOperationException>(() => NativeRecoveryPackage.ReadFrom(output, fixture.Passphrase));
        Assert.Equal(data, output.ToArray());
    }

    [Fact]
    public void CancellationNeverReturnsARecoveryPackageOrWritesToAnUntouchedDestination()
    {
        using var fixture = new Fixture();
        using var package = NativeRecoveryPackage.Create(fixture.Metadata, fixture.Captures, fixture.Passphrase);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        using var destination = new MemoryStream();
        Assert.Throws<OperationCanceledException>(() => package.WriteTo(destination, cancelled.Token)); Assert.Equal(0, destination.Length);
        package.WriteTo(destination); destination.Position = 0;
        Assert.Throws<OperationCanceledException>(() => NativeRecoveryPackage.ReadFrom(destination, fixture.Passphrase, cancelled.Token));
    }

    [Theory]
    [InlineData("manifest")]
    [InlineData("artifact")]
    [InlineData("footer")]
    public void MidWriteFailureLeavesOnlyAnUnusablePartialPackage(string stage)
    {
        using var fixture = new Fixture();
        using var package = NativeRecoveryPackage.Create(fixture.Metadata, fixture.Captures, fixture.Passphrase);
        using var complete = new MemoryStream(); package.WriteTo(complete); var bytes = complete.ToArray();
        var cutoff = stage switch
        {
            "manifest" => 180,
            "artifact" => 164 + BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(160, 4)) + 10,
            "footer" => bytes.Length - 4,
            _ => throw new InvalidOperationException()
        };
        using var destination = new FaultingStream(cutoff);
        var error = Assert.Throws<InvalidOperationException>(() => package.WriteTo(destination)); Assert.Null(error.InnerException);
        Assert.Equal(cutoff, destination.Length);
        destination.Position = 0;
        Assert.Throws<InvalidOperationException>(() => NativeRecoveryPackage.ReadFrom(destination, fixture.Passphrase));
    }

    [Fact]
    public void CancellationDuringArtifactReadsDoesNotReturnAPartiallyVerifiedPackage()
    {
        using var fixture = new Fixture();
        using var package = NativeRecoveryPackage.Create(fixture.Metadata, fixture.Captures, fixture.Passphrase);
        using var output = new MemoryStream(); package.WriteTo(output); var bytes = output.ToArray();
        using var cancellation = new CancellationTokenSource();
        var cutoff = 164 + BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(160, 4)) + 1;
        using var source = new CancellingStream(bytes, cutoff, cancellation);
        Assert.Throws<OperationCanceledException>(() => NativeRecoveryPackage.ReadFrom(source, fixture.Passphrase, cancellation.Token));
    }

    [Fact]
    public void DisposedPackageCannotWriteOrDecryptAndDoesNotOwnTheCallersBuffers()
    {
        using var fixture = new Fixture();
        var package = NativeRecoveryPackage.Create(fixture.Metadata, fixture.Captures, fixture.Passphrase);
        var id = package.Manifest.Artifacts[0].Id; package.Dispose(); package.Dispose();
        using var output = new MemoryStream();
        Assert.Throws<ObjectDisposedException>(() => package.WriteTo(output));
        Assert.Throws<ObjectDisposedException>(() => package.OpenArtifact(id, fixture.Passphrase));
        Assert.All(fixture.Captures, capture => Assert.Equal("synthetic-only-financial-payload", Encoding.UTF8.GetString(capture.Plaintext.Span)));
    }

    private static string Root()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        { if (File.Exists(Path.Combine(directory.FullName, "FinancialAssistant.Backend.sln"))) { return directory.FullName; } }
        throw new DirectoryNotFoundException();
    }

    private sealed class FaultingStream(int cutoff) : MemoryStream
    {
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            var count = Math.Min(buffer.Length, cutoff - checked((int)Position));
            base.Write(buffer[..count]);
            if (count != buffer.Length) { throw new IOException("Synthetic write interruption."); }
        }
    }

    private sealed class CancellingStream(byte[] bytes, int cutoff, CancellationTokenSource cancellation) : MemoryStream(bytes, writable: false)
    {
        public override int Read(Span<byte> buffer)
        {
            var count = base.Read(buffer);
            if (Position >= cutoff) { cancellation.Cancel(); }
            return count;
        }
    }

    private sealed class Fixture : IDisposable
    {
        public byte[] Passphrase { get; } = Encoding.UTF8.GetBytes("synthetic-independent-operator-unlock-material-only");
        public NativeRecoveryManifestDocument Metadata { get; } = new()
        {
            SchemaVersion = 1,
            SourceInstallationId = Guid.NewGuid(),
            BackupId = Guid.NewGuid(),
            ManifestId = Guid.NewGuid(),
            ConsistencyBoundaryId = Guid.NewGuid(),
            CapturedAtUtc = DateTimeOffset.Parse("2026-11-03T08:00:00Z"),
            ApplicationVersion = "1.0.0",
            InstallerManifestSha256 = new string('A', 64),
            PostgresMajorVersion = 18,
            BrokerPolicy = NativeRecoveryBrokerPolicy.DrainedThenRecreate,
            SearchPolicy = NativeRecoverySearchPolicy.RebuildFromOwnedState
        };
        public NativeRecoveryCapture[] Captures { get; } = NativeRecoveryManifest.RequiredResources.Select(resource => new NativeRecoveryCapture
        {
            Owner = resource.Owner,
            Kind = resource.Kind,
            Plaintext = "synthetic-only-financial-payload"u8.ToArray(),
            MigrationChecksum = resource.Kind == NativeRecoveryArtifactKind.Database ? new string('B', 64) : null
        }).ToArray();
        public void Dispose()
        {
            CryptographicOperations.ZeroMemory(Passphrase);
            foreach (var capture in Captures) { if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(capture.Plaintext, out var bytes)) { CryptographicOperations.ZeroMemory(bytes.AsSpan()); } }
        }
    }
}
