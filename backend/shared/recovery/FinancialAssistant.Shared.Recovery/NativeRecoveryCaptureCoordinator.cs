using System.Security.Cryptography;
using FinancialAssistant.Shared.Hosting;

namespace FinancialAssistant.Shared.Recovery;

public interface INativeRecoveryCaptureSource
{
    ValueTask<INativeRecoveryCaptureBoundary> AcquireAsync(Guid sourceInstallationId, Guid boundaryId, CancellationToken token);
}

// The privileged adapter owns its durable journal and exclusive installation lease.
// Disposing releases that lease, but MUST NOT restart writers or reopen ingress.
public interface INativeRecoveryCaptureBoundary : IAsyncDisposable
{
    ValueTask FreezeIngressAndScheduledWorkAsync(CancellationToken token);
    ValueTask DrainBrokerAsync(CancellationToken token);
    ValueTask StopWritersAsync(CancellationToken token);
    ValueTask<NativeRecoveryQuiescence> InspectAsync(CancellationToken token);
    ValueTask<NativeRecoveryCapturedResource> CaptureAsync(NativeRecoveryResource resource, CancellationToken token);
}

public sealed record NativeRecoveryQuiescence(Guid SourceInstallationId, Guid BoundaryId, DateTimeOffset ObservedAtUtc,
    bool IngressFrozen, bool ScheduledWorkFrozen, bool SourceOwnershipVerified, bool BrokerTopologyVerified,
    long ReadyMessages, long UnacknowledgedMessages, long ActiveDatabaseClients, long ActiveReceiptWriters,
    IReadOnlyList<string> StoppedWriters);

public sealed class NativeRecoveryCapturedResource : IDisposable
{
    private readonly byte[] plaintext;
    private bool disposed;

    // Ownership of the supplied mutable buffer transfers to this object, including on validation failure.
    public NativeRecoveryCapturedResource(NativeRecoveryResource resource, byte[] plaintext, int dataSchemaVersion = 1, string? migrationChecksum = null)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        this.plaintext = plaintext;
        Resource = resource; DataSchemaVersion = dataSchemaVersion; MigrationChecksum = migrationChecksum;
    }

    public NativeRecoveryResource Resource { get; }
    public int DataSchemaVersion { get; }
    public string? MigrationChecksum { get; }
    public ReadOnlyMemory<byte> Plaintext { get { ObjectDisposedException.ThrowIf(disposed, this); return plaintext; } }
    public void Dispose() { if (!disposed) { disposed = true; CryptographicOperations.ZeroMemory(plaintext); } }
    public override string ToString() => "Native recovery captured resource (payload omitted).";
}

public sealed class NativeRecoveryCaptureCoordinator(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private static readonly string[] Writers = NativeServiceCatalog.Services.Select(service => service.Id).Append("financial-summary").ToArray();

    public async Task<NativeRecoveryPackage> CaptureAsync(INativeRecoveryCaptureSource source, NativeRecoveryManifestDocument metadata,
        ReadOnlyMemory<byte> operatorPassphrase, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(metadata);
        NativeRecoveryPackage? package = null;
        var owned = new List<NativeRecoveryCapturedResource>();
        try
        {
            token.ThrowIfCancellationRequested();
            if (metadata.Artifacts is null || metadata.Artifacts.Count != 0 || operatorPassphrase.Length is < 32 or > 1024) { throw Invalid(); }
            // Validate and snapshot caller metadata before the first privileged action.
            var snapshot = Copy(metadata, clock.GetUtcNow(), NativeRecoveryManifest.RequiredResources.Select(resource => new NativeRecoveryArtifactDescriptor
            {
                Id = Guid.NewGuid(),
                Owner = resource.Owner,
                Kind = resource.Kind,
                DataSchemaVersion = 1,
                CiphertextBytes = NativeRecoveryArtifactEncryption.EnvelopeOverheadBytes + 1,
                Sha256 = new string('0', 64),
                MigrationChecksum = resource.Kind == NativeRecoveryArtifactKind.Database ? new string('0', 64) : null
            }).ToList());
            _ = NativeRecoveryManifest.Create(snapshot);
            await using (var boundary = await source.AcquireAsync(snapshot.SourceInstallationId, snapshot.ConsistencyBoundaryId, token) ?? throw Invalid())
            {
                token.ThrowIfCancellationRequested();
                await boundary.FreezeIngressAndScheduledWorkAsync(token); token.ThrowIfCancellationRequested();
                await boundary.DrainBrokerAsync(token); token.ThrowIfCancellationRequested();
                await boundary.StopWritersAsync(token); token.ThrowIfCancellationRequested();
                var capturedAt = clock.GetUtcNow();
                await Verify(boundary, snapshot, capturedAt, token);
                long total = 0;
                var captures = new List<NativeRecoveryCapture>();
                foreach (var resource in NativeRecoveryManifest.RequiredResources)
                {
                    token.ThrowIfCancellationRequested();
                    var capture = await boundary.CaptureAsync(resource, token) ?? throw Invalid();
                    owned.Add(capture);
                    token.ThrowIfCancellationRequested();
                    total += capture.Plaintext.Length;
                    if (capture.Resource != resource || capture.Plaintext.Length is < 1 or > NativeRecoveryArtifactEncryption.MaximumPlaintextBytes ||
                        total > NativeRecoveryPackage.MaximumCaptureBytes || capture.DataSchemaVersion != 1 ||
                        (resource.Kind == NativeRecoveryArtifactKind.Database ? !Digest(capture.MigrationChecksum) : capture.MigrationChecksum is not null)) { throw Invalid(); }
                    captures.Add(new()
                    {
                        Owner = resource.Owner,
                        Kind = resource.Kind,
                        Plaintext = capture.Plaintext,
                        DataSchemaVersion = capture.DataSchemaVersion,
                        MigrationChecksum = capture.MigrationChecksum
                    });
                    await Verify(boundary, snapshot, capturedAt, token);
                }
                package = NativeRecoveryPackage.Create(Copy(snapshot, capturedAt, new()), captures, operatorPassphrase.Span);
                token.ThrowIfCancellationRequested();
                await Verify(boundary, snapshot, capturedAt, token);
            }
            token.ThrowIfCancellationRequested();
            return package;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { package?.Dispose(); throw new OperationCanceledException(token); }
        catch { package?.Dispose(); throw Invalid(); }
        finally { foreach (var capture in owned) { capture.Dispose(); } }
    }

    private async ValueTask Verify(INativeRecoveryCaptureBoundary boundary, NativeRecoveryManifestDocument metadata, DateTimeOffset began, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var requestedAt = clock.GetUtcNow();
        var proof = await boundary.InspectAsync(token); var now = clock.GetUtcNow();
        token.ThrowIfCancellationRequested();
        if (proof is null || proof.SourceInstallationId != metadata.SourceInstallationId || proof.BoundaryId != metadata.ConsistencyBoundaryId ||
            proof.ObservedAtUtc.Offset != TimeSpan.Zero || proof.ObservedAtUtc < began || proof.ObservedAtUtc < requestedAt ||
            proof.ObservedAtUtc > now || now - proof.ObservedAtUtc > TimeSpan.FromSeconds(5) ||
            !proof.IngressFrozen || !proof.ScheduledWorkFrozen || !proof.SourceOwnershipVerified || !proof.BrokerTopologyVerified ||
            proof.ReadyMessages != 0 || proof.UnacknowledgedMessages != 0 || proof.ActiveDatabaseClients != 0 || proof.ActiveReceiptWriters != 0 ||
            proof.StoppedWriters is null || proof.StoppedWriters.Count != Writers.Length ||
            proof.StoppedWriters.Distinct(StringComparer.Ordinal).Count() != Writers.Length || !proof.StoppedWriters.ToHashSet(StringComparer.Ordinal).SetEquals(Writers))
        { throw Invalid(); }
    }

    private static bool Digest(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static NativeRecoveryManifestDocument Copy(NativeRecoveryManifestDocument metadata, DateTimeOffset capturedAt, List<NativeRecoveryArtifactDescriptor> artifacts) => new()
    {
        SchemaVersion = metadata.SchemaVersion,
        SourceInstallationId = metadata.SourceInstallationId,
        BackupId = metadata.BackupId,
        ManifestId = metadata.ManifestId,
        ConsistencyBoundaryId = metadata.ConsistencyBoundaryId,
        CapturedAtUtc = capturedAt,
        ApplicationVersion = metadata.ApplicationVersion,
        InstallerManifestSha256 = metadata.InstallerManifestSha256,
        PostgresMajorVersion = metadata.PostgresMajorVersion,
        BrokerPolicy = metadata.BrokerPolicy,
        SearchPolicy = metadata.SearchPolicy,
        Artifacts = artifacts
    };

    private static InvalidOperationException Invalid() => new("Native recovery capture boundary or resource is unavailable or invalid.");
}
