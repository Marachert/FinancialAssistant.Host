using System.Buffers.Binary;
using System.Security.Cryptography;

namespace FinancialAssistant.Shared.Recovery;

public sealed class NativeRecoveryCapture
{
    public required string Owner { get; init; }
    public required NativeRecoveryArtifactKind Kind { get; init; }
    public required ReadOnlyMemory<byte> Plaintext { get; init; }
    public int DataSchemaVersion { get; init; } = 1;
    public string? MigrationChecksum { get; init; }
    public override string ToString() => "Native recovery capture (payload omitted).";
}

public sealed class NativeRecoveryPackage : IDisposable
{
    public const int MaximumPackageBytes = 128 * 1024 * 1024;
    private const int HeaderBytes = 164;
    internal static long MaximumCaptureBytes => MaximumPackageBytes - HeaderBytes - NativeRecoveryManifest.MaximumDocumentBytes -
        NativeRecoveryArtifactEncryption.EnvelopeOverheadBytes - 8 - (long)NativeRecoveryManifest.RequiredResources.Count * NativeRecoveryArtifactEncryption.EnvelopeOverheadBytes;
    private static ReadOnlySpan<byte> Magic => "FAPACK01"u8;
    private static ReadOnlySpan<byte> Complete => "FADONE01"u8;
    private readonly byte[] header;
    private readonly byte[] manifestCiphertext;
    private readonly Dictionary<Guid, byte[]> artifacts;
    private bool disposed;

    private NativeRecoveryPackage(NativeRecoveryManifest manifest, byte[] header, byte[] manifestCiphertext, Dictionary<Guid, byte[]> artifacts)
    { Manifest = manifest; this.header = header; this.manifestCiphertext = manifestCiphertext; this.artifacts = artifacts; }

    public NativeRecoveryManifest Manifest { get; }

    public static NativeRecoveryPackage Create(NativeRecoveryManifestDocument metadata,
        IReadOnlyList<NativeRecoveryCapture> captures, ReadOnlySpan<byte> operatorPassphrase)
    {
        ArgumentNullException.ThrowIfNull(metadata); ArgumentNullException.ThrowIfNull(captures);
        var ciphertext = new Dictionary<Guid, byte[]>();
        byte[]? key = null;
        try
        {
            if (metadata.Artifacts is null || metadata.Artifacts.Count != 0 || captures.Count != NativeRecoveryManifest.RequiredResources.Count ||
                operatorPassphrase.Length is < 32 or > 1024 || captures.Any(capture => capture is null || capture.Plaintext.Length is < 1 or > NativeRecoveryArtifactEncryption.MaximumPlaintextBytes))
            { throw Invalid(); }
            var descriptors = captures.Select(capture => new NativeRecoveryArtifactDescriptor
            {
                Id = Guid.NewGuid(),
                Owner = capture.Owner,
                Kind = capture.Kind,
                DataSchemaVersion = capture.DataSchemaVersion,
                MigrationChecksum = capture.MigrationChecksum,
                CiphertextBytes = capture.Plaintext.Length + NativeRecoveryArtifactEncryption.EnvelopeOverheadBytes,
                Sha256 = new string('0', 64)
            }).ToList();
            var provisional = NativeRecoveryManifest.Create(Document(metadata, descriptors));
            // Reserve the largest permitted manifest before allocating artifact ciphertext.
            if ((long)HeaderBytes + NativeRecoveryManifest.MaximumDocumentBytes + NativeRecoveryArtifactEncryption.EnvelopeOverheadBytes + Complete.Length +
                provisional.Artifacts.Sum(artifact => (long)artifact.CiphertextBytes) > MaximumPackageBytes) { throw Invalid(); }
            key = NativeRecoveryKeyWrapping.CreateRecoveryKey();
            for (var index = 0; index < captures.Count; index++)
            {
                var artifact = descriptors[index];
                var bytes = NativeRecoveryArtifactEncryption.Seal(captures[index].Plaintext.Span, key,
                    NativeRecoveryContext.Create(metadata.SourceInstallationId, metadata.BackupId, artifact.Id, artifact.Owner, artifact.Kind));
                ciphertext.Add(artifact.Id, bytes);
                descriptors[index] = artifact with { Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
            }
            var manifest = NativeRecoveryManifest.Create(Document(metadata, descriptors));
            var encryptedManifest = manifest.Seal(key);
            var header = new byte[HeaderBytes]; Magic.CopyTo(header);
            metadata.SourceInstallationId.TryWriteBytes(header.AsSpan(8, 16)); metadata.BackupId.TryWriteBytes(header.AsSpan(24, 16));
            metadata.ManifestId.TryWriteBytes(header.AsSpan(40, 16));
            NativeRecoveryKeyWrapping.Wrap(metadata.SourceInstallationId, metadata.BackupId, key, operatorPassphrase).CopyTo(header, 56);
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(160, 4), encryptedManifest.Length);
            return new(manifest, header, encryptedManifest, ciphertext);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or CryptographicException or OverflowException)
        {
            foreach (var bytes in ciphertext.Values) { CryptographicOperations.ZeroMemory(bytes); }
            throw Invalid();
        }
        finally { if (key is not null) { CryptographicOperations.ZeroMemory(key); } }
    }

    public void WriteTo(Stream destination, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this); ArgumentNullException.ThrowIfNull(destination);
        try
        {
            if (!destination.CanWrite || !destination.CanSeek || destination.Position != 0 || destination.Length != 0) { throw Invalid(); }
            token.ThrowIfCancellationRequested(); destination.Write(header); destination.Write(manifestCiphertext);
            foreach (var artifact in Manifest.Artifacts) { token.ThrowIfCancellationRequested(); destination.Write(artifacts[artifact.Id]); }
            token.ThrowIfCancellationRequested(); destination.Write(Complete); destination.Flush();
            if (destination is FileStream file) { file.Flush(flushToDisk: true); }
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or NotSupportedException)
        { throw Invalid(); }
    }

    public static NativeRecoveryPackage ReadFrom(Stream source, ReadOnlySpan<byte> operatorPassphrase, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var ciphertext = new Dictionary<Guid, byte[]>(); byte[]? key = null;
        try
        {
            if (!source.CanRead || !source.CanSeek || source.Position != 0 || source.Length is < HeaderBytes + 37 + 8 or > MaximumPackageBytes) { throw Invalid(); }
            token.ThrowIfCancellationRequested();
            var header = new byte[HeaderBytes]; source.ReadExactly(header);
            if (!header.AsSpan(0, 8).SequenceEqual(Magic)) { throw Invalid(); }
            var installation = new Guid(header.AsSpan(8, 16)); var backup = new Guid(header.AsSpan(24, 16)); var manifestId = new Guid(header.AsSpan(40, 16));
            var size = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(160, 4));
            if (size is <= NativeRecoveryArtifactEncryption.EnvelopeOverheadBytes or > NativeRecoveryManifest.MaximumDocumentBytes + NativeRecoveryArtifactEncryption.EnvelopeOverheadBytes) { throw Invalid(); }
            key = NativeRecoveryKeyWrapping.Unwrap(installation, backup, header.AsSpan(56, NativeRecoveryKeyWrapping.EnvelopeBytes), operatorPassphrase);
            var encryptedManifest = new byte[size]; source.ReadExactly(encryptedManifest);
            var manifest = NativeRecoveryManifest.Open(encryptedManifest, key,
                NativeRecoveryContext.Create(installation, backup, manifestId, "installation", NativeRecoveryArtifactKind.Manifest));
            var expected = (long)HeaderBytes + size + Complete.Length + manifest.Artifacts.Sum(artifact => (long)artifact.CiphertextBytes);
            if (expected > MaximumPackageBytes || expected != source.Length) { throw Invalid(); }
            foreach (var artifact in manifest.Artifacts)
            {
                token.ThrowIfCancellationRequested();
                var bytes = new byte[artifact.CiphertextBytes]; source.ReadExactly(bytes); ciphertext.Add(artifact.Id, bytes);
                var plaintext = manifest.OpenArtifact(artifact.Id, bytes, key);
                CryptographicOperations.ZeroMemory(plaintext);
            }
            Span<byte> complete = stackalloc byte[8]; source.ReadExactly(complete);
            if (!complete.SequenceEqual(Complete) || source.Position != expected || source.Length != expected) { throw Invalid(); }
            token.ThrowIfCancellationRequested();
            return new(manifest, header, encryptedManifest, ciphertext);
        }
        catch (OperationCanceledException)
        {
            foreach (var bytes in ciphertext.Values) { CryptographicOperations.ZeroMemory(bytes); }
            throw;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or InvalidOperationException or NotSupportedException or CryptographicException or OverflowException)
        {
            foreach (var bytes in ciphertext.Values) { CryptographicOperations.ZeroMemory(bytes); }
            throw Invalid();
        }
        finally { if (key is not null) { CryptographicOperations.ZeroMemory(key); } }
    }

    public byte[] OpenArtifact(Guid artifactId, ReadOnlySpan<byte> operatorPassphrase)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!artifacts.TryGetValue(artifactId, out var bytes)) { throw Invalid(); }
        var key = NativeRecoveryKeyWrapping.Unwrap(Manifest.SourceInstallationId, Manifest.BackupId,
            header.AsSpan(56, NativeRecoveryKeyWrapping.EnvelopeBytes), operatorPassphrase);
        try { return Manifest.OpenArtifact(artifactId, bytes, key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    public void Dispose()
    {
        if (disposed) { return; }
        disposed = true;
        CryptographicOperations.ZeroMemory(header); CryptographicOperations.ZeroMemory(manifestCiphertext);
        foreach (var bytes in artifacts.Values) { CryptographicOperations.ZeroMemory(bytes); }
        artifacts.Clear();
    }

    public override string ToString() => "Native portable encrypted package (payload and unlock material omitted).";

    private static NativeRecoveryManifestDocument Document(NativeRecoveryManifestDocument metadata, List<NativeRecoveryArtifactDescriptor> artifacts) => new()
    {
        SchemaVersion = metadata.SchemaVersion,
        SourceInstallationId = metadata.SourceInstallationId,
        BackupId = metadata.BackupId,
        ManifestId = metadata.ManifestId,
        ConsistencyBoundaryId = metadata.ConsistencyBoundaryId,
        CapturedAtUtc = metadata.CapturedAtUtc,
        ApplicationVersion = metadata.ApplicationVersion,
        InstallerManifestSha256 = metadata.InstallerManifestSha256,
        PostgresMajorVersion = metadata.PostgresMajorVersion,
        BrokerPolicy = metadata.BrokerPolicy,
        SearchPolicy = metadata.SearchPolicy,
        Artifacts = artifacts
    };

    private static InvalidOperationException Invalid() => new("Native portable recovery package is incomplete, unavailable or invalid.");
}
