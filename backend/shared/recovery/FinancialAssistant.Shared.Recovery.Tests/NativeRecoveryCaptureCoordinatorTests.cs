using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Shared.Hosting;
using Xunit;

namespace FinancialAssistant.Shared.Recovery.Tests;

public sealed class NativeRecoveryCaptureCoordinatorTests
{
    [Fact]
    public async Task OrderedCaptureReturnsAnIndependentlyReadableCompletePackageAndClearsPlaintext()
    {
        using var fixture = new Fixture();
        using var package = await fixture.Coordinator.CaptureAsync(fixture, fixture.Metadata, fixture.Passphrase);
        Assert.Equal(new[] { "acquire", "freeze", "drain", "stop", "inspect:1" }, fixture.Events.Take(5));
        Assert.Equal("dispose", fixture.Events[^1]); Assert.Equal(32, fixture.Inspections);
        Assert.Equal(30, fixture.Buffers.Count); AssertCleared(fixture);
        Assert.Equal(fixture.Now, package.Manifest.CapturedAtUtc);
        Assert.Equal(fixture.Metadata.ConsistencyBoundaryId, package.Manifest.ConsistencyBoundaryId);
        using var stream = new MemoryStream(); package.WriteTo(stream); stream.Position = 0;
        using var reopened = NativeRecoveryPackage.ReadFrom(stream, fixture.Passphrase);
        foreach (var artifact in reopened.Manifest.Artifacts)
        {
            var plaintext = reopened.OpenArtifact(artifact.Id, fixture.Passphrase);
            try { Assert.Equal("synthetic-" + artifact.Owner + "-" + artifact.Kind, Encoding.UTF8.GetString(plaintext)); }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
        }
        Assert.Equal(1, fixture.Disposals); Assert.Equal(30, reopened.Manifest.Artifacts.Count);
    }

    [Theory]
    [InlineData("acquire")]
    [InlineData("freeze")]
    [InlineData("drain")]
    [InlineData("stop")]
    [InlineData("inspect:1")]
    [InlineData("capture:0")]
    [InlineData("capture:15")]
    [InlineData("capture:29")]
    [InlineData("inspect:16")]
    [InlineData("inspect:31")]
    [InlineData("inspect:32")]
    [InlineData("dispose")]
    public async Task FaultsNeverReturnAPackageAndReleaseOnlyTheAcquiredLease(string stage)
    {
        using var fixture = new Fixture { FailAt = stage };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.CaptureAsync(fixture, fixture.Metadata, fixture.Passphrase));
        Assert.Null(error.InnerException); Assert.DoesNotContain("private-fixture-diagnostic", error.ToString());
        Assert.Equal(stage == "acquire" ? 0 : 1, fixture.Disposals); AssertCleared(fixture);
        Assert.DoesNotContain("resume", fixture.Events);
    }

    [Theory]
    [InlineData("acquire")]
    [InlineData("freeze")]
    [InlineData("drain")]
    [InlineData("stop")]
    [InlineData("capture:0")]
    [InlineData("capture:15")]
    [InlineData("capture:29")]
    [InlineData("inspect:32")]
    [InlineData("dispose")]
    public async Task CancellationAtEveryPhaseClearsCapturesAndDoesNotReopenIngress(string stage)
    {
        using var fixture = new Fixture { CancelAt = stage };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Coordinator.CaptureAsync(fixture, fixture.Metadata, fixture.Passphrase, fixture.Cancellation.Token));
        Assert.Equal(stage == "acquire" ? 0 : 1, fixture.Disposals); AssertCleared(fixture);
        Assert.DoesNotContain("resume", fixture.Events);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("boundary")]
    [InlineData("old")]
    [InlineData("future")]
    [InlineData("stale")]
    [InlineData("offset")]
    [InlineData("ingress")]
    [InlineData("scheduled")]
    [InlineData("ownership")]
    [InlineData("topology")]
    [InlineData("ready")]
    [InlineData("unacknowledged")]
    [InlineData("database-clients")]
    [InlineData("receipt-writers")]
    [InlineData("negative-count")]
    [InlineData("missing-writer")]
    [InlineData("duplicate-writer")]
    [InlineData("foreign-writer")]
    [InlineData("null-writers")]
    [InlineData("null-proof")]
    public async Task InvalidOrDriftingBoundaryEvidenceRejectsTheWholeCapture(string mutation)
    {
        using var fixture = new Fixture { ProofMutation = mutation, MutateOnInspection = 16 };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.CaptureAsync(fixture, fixture.Metadata, fixture.Passphrase));
        Assert.Equal(15, fixture.Buffers.Count); Assert.Equal(1, fixture.Disposals); AssertCleared(fixture);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("empty")]
    [InlineData("schema")]
    [InlineData("checksum")]
    [InlineData("configuration-checksum")]
    [InlineData("null")]
    public async Task InvalidResourceStopsFurtherCaptureAndClearsTransferredBuffers(string mutation)
    {
        using var fixture = new Fixture { CaptureMutation = mutation };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.CaptureAsync(fixture, fixture.Metadata, fixture.Passphrase));
        Assert.InRange(fixture.Buffers.Count, 0, 2); Assert.Equal(1, fixture.Disposals); AssertCleared(fixture);
    }

    [Fact]
    public async Task InvalidMetadataUnlockAndPreCancellationDoNotAcquireOrMutateTheSource()
    {
        using var fixture = new Fixture();
        fixture.Metadata.Artifacts.Add(new());
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.CaptureAsync(fixture, fixture.Metadata, fixture.Passphrase));
        fixture.Metadata.Artifacts.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.CaptureAsync(fixture, fixture.Metadata, new byte[31]));
        fixture.Cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Coordinator.CaptureAsync(fixture, fixture.Metadata, fixture.Passphrase, fixture.Cancellation.Token));
        Assert.Empty(fixture.Events); Assert.Empty(fixture.Buffers);
    }

    [Fact]
    public async Task AcquireFailureWithForeignCancellationIsSanitizedRatherThanReportedAsCallerCancellation()
    {
        using var fixture = new Fixture { FailAt = "foreign-cancellation" };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.CaptureAsync(fixture, fixture.Metadata, fixture.Passphrase));
        Assert.Null(error.InnerException); Assert.Equal(0, fixture.Disposals);
    }

    [Fact]
    public async Task CancelledAcquisitionThatReturnsALeaseDoesNotFreezeOrDrainTheSource()
    {
        using var fixture = new Fixture { CancelAt = "acquire", IgnoreAcquisitionCancellation = true };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Coordinator.CaptureAsync(fixture, fixture.Metadata, fixture.Passphrase, fixture.Cancellation.Token));
        Assert.Equal(new[] { "acquire", "dispose" }, fixture.Events); Assert.Empty(fixture.Buffers);
    }

    [Theory]
    [InlineData("individual-limit", 1)]
    [InlineData("aggregate-limit", 2)]
    public async Task OversizedCaptureIsRefusedBeforeRetainingFurtherResources(string mutation, int buffers)
    {
        using var fixture = new Fixture { CaptureMutation = mutation };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.CaptureAsync(fixture, fixture.Metadata, fixture.Passphrase));
        Assert.Equal(buffers, fixture.Buffers.Count); Assert.Equal(1, fixture.Disposals); AssertCleared(fixture);
    }

    [Fact]
    public async Task CallerMetadataChangesAfterAcquisitionDoNotChangeTheValidatedSnapshot()
    {
        using var fixture = new Fixture { MutateMetadataOnAcquire = true };
        using var package = await fixture.Coordinator.CaptureAsync(fixture, fixture.Metadata, fixture.Passphrase);
        Assert.Equal(30, package.Manifest.Artifacts.Count); Assert.Single(fixture.Metadata.Artifacts); AssertCleared(fixture);
    }

    [Fact]
    public async Task RecentCachedObservationFromBeforeTheCurrentInspectionIsRefused()
    {
        using var fixture = new Fixture { ProofMutation = "cached", MutateOnInspection = 16, AdvanceClockOnCapture = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Coordinator.CaptureAsync(fixture, fixture.Metadata, fixture.Passphrase));
        Assert.Equal(15, fixture.Buffers.Count); Assert.Equal(1, fixture.Disposals); AssertCleared(fixture);
    }

    [Fact]
    public void CaptureBufferOwnershipIsExplicitAndDisposalIsIdempotent()
    {
        var bytes = "synthetic-sensitive-content"u8.ToArray();
        var capture = new NativeRecoveryCapturedResource(new("income", NativeRecoveryArtifactKind.Database), bytes);
        Assert.DoesNotContain("synthetic", capture.ToString()); capture.Dispose(); capture.Dispose();
        Assert.All(bytes, value => Assert.Equal((byte)0, value));
        Assert.Throws<ObjectDisposedException>(() => capture.Plaintext);
    }

    private static void AssertCleared(Fixture fixture) => Assert.All(fixture.Buffers, bytes => Assert.Equal(-1, bytes.AsSpan().IndexOfAnyExcept((byte)0)));

    private sealed class Fixture : TimeProvider, INativeRecoveryCaptureSource, INativeRecoveryCaptureBoundary, IDisposable
    {
        public DateTimeOffset Now { get; private set; } = DateTimeOffset.Parse("2026-11-04T10:30:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
        public NativeRecoveryCaptureCoordinator Coordinator => new(this);
        public byte[] Passphrase { get; } = Encoding.UTF8.GetBytes("synthetic-independent-operator-unlock-only");
        public CancellationTokenSource Cancellation { get; } = new();
        public NativeRecoveryManifestDocument Metadata { get; } = new()
        {
            SchemaVersion = 1,
            SourceInstallationId = Guid.NewGuid(),
            BackupId = Guid.NewGuid(),
            ManifestId = Guid.NewGuid(),
            ConsistencyBoundaryId = Guid.NewGuid(),
            ApplicationVersion = "1.0.0",
            InstallerManifestSha256 = new string('A', 64),
            PostgresMajorVersion = 18,
            BrokerPolicy = NativeRecoveryBrokerPolicy.DrainedThenRecreate,
            SearchPolicy = NativeRecoverySearchPolicy.RebuildFromOwnedState
        };
        public string? FailAt { get; init; }
        public string? CancelAt { get; init; }
        public string? ProofMutation { get; init; }
        public int MutateOnInspection { get; init; } = 1;
        public string? CaptureMutation { get; init; }
        public bool IgnoreAcquisitionCancellation { get; init; }
        public bool MutateMetadataOnAcquire { get; init; }
        public bool AdvanceClockOnCapture { get; init; }
        public int Inspections { get; private set; }
        public int Disposals { get; private set; }
        public List<string> Events { get; } = new();
        public List<byte[]> Buffers { get; } = new();
        public ValueTask<INativeRecoveryCaptureBoundary> AcquireAsync(Guid installation, Guid boundary, CancellationToken token)
        {
            Assert.Equal(Metadata.SourceInstallationId, installation); Assert.Equal(Metadata.ConsistencyBoundaryId, boundary);
            Step("acquire");
            if (!IgnoreAcquisitionCancellation) { token.ThrowIfCancellationRequested(); }
            if (MutateMetadataOnAcquire) { Metadata.Artifacts.Add(new()); }
            if (FailAt == "foreign-cancellation") { throw new OperationCanceledException("private-fixture-diagnostic"); }
            return ValueTask.FromResult<INativeRecoveryCaptureBoundary>(this);
        }
        public ValueTask FreezeIngressAndScheduledWorkAsync(CancellationToken token) { Step("freeze"); return ValueTask.CompletedTask; }
        public ValueTask DrainBrokerAsync(CancellationToken token) { Step("drain"); return ValueTask.CompletedTask; }
        public ValueTask StopWritersAsync(CancellationToken token) { Step("stop"); return ValueTask.CompletedTask; }
        public ValueTask<NativeRecoveryQuiescence> InspectAsync(CancellationToken token)
        {
            Step("inspect:" + ++Inspections);
            var proof = new NativeRecoveryQuiescence(Metadata.SourceInstallationId, Metadata.ConsistencyBoundaryId, Now,
                true, true, true, true, 0, 0, 0, 0, NativeServiceCatalog.Services.Select(service => service.Id).Append("financial-summary").ToArray());
            if (Inspections == MutateOnInspection)
            {
                proof = ProofMutation switch
                {
                    "source" => proof with { SourceInstallationId = Guid.NewGuid() },
                    "boundary" => proof with { BoundaryId = Guid.NewGuid() },
                    "old" => proof with { ObservedAtUtc = Now.AddTicks(-1) },
                    "cached" => proof with { ObservedAtUtc = Now.AddMilliseconds(-1) },
                    "future" => proof with { ObservedAtUtc = Now.AddTicks(1) },
                    "offset" => proof with { ObservedAtUtc = Now.ToOffset(TimeSpan.FromHours(1)) },
                    "ingress" => proof with { IngressFrozen = false },
                    "scheduled" => proof with { ScheduledWorkFrozen = false },
                    "ownership" => proof with { SourceOwnershipVerified = false },
                    "topology" => proof with { BrokerTopologyVerified = false },
                    "ready" => proof with { ReadyMessages = 1 },
                    "unacknowledged" => proof with { UnacknowledgedMessages = 1 },
                    "database-clients" => proof with { ActiveDatabaseClients = 1 },
                    "receipt-writers" => proof with { ActiveReceiptWriters = 1 },
                    "negative-count" => proof with { ReadyMessages = -1 },
                    "missing-writer" => proof with { StoppedWriters = proof.StoppedWriters.Skip(1).ToArray() },
                    "duplicate-writer" => proof with { StoppedWriters = proof.StoppedWriters.Skip(1).Append(proof.StoppedWriters[1]).ToArray() },
                    "foreign-writer" => proof with { StoppedWriters = proof.StoppedWriters.Skip(1).Append("foreign").ToArray() },
                    "null-writers" => proof with { StoppedWriters = null! },
                    "null-proof" => null!,
                    _ => proof
                };
                if (ProofMutation == "stale") { Now = Now.AddSeconds(6); }
            }
            return ValueTask.FromResult(proof);
        }
        public ValueTask<NativeRecoveryCapturedResource> CaptureAsync(NativeRecoveryResource resource, CancellationToken token)
        {
            Step("capture:" + Buffers.Count);
            if (AdvanceClockOnCapture) { Now = Now.AddMilliseconds(1); }
            if (CaptureMutation == "null") { return ValueTask.FromResult<NativeRecoveryCapturedResource>(null!); }
            var bytes = CaptureMutation switch
            {
                "empty" => Array.Empty<byte>(),
                "individual-limit" => new byte[NativeRecoveryArtifactEncryption.MaximumPlaintextBytes + 1],
                "aggregate-limit" => new byte[NativeRecoveryArtifactEncryption.MaximumPlaintextBytes],
                _ => Encoding.UTF8.GetBytes("synthetic-" + resource.Owner + "-" + resource.Kind)
            };
            if (bytes.Length > 0) { bytes[^1] = bytes[^1] == 0 ? (byte)1 : bytes[^1]; }
            Buffers.Add(bytes);
            return ValueTask.FromResult(new NativeRecoveryCapturedResource(
                CaptureMutation == "foreign" ? new("foreign", resource.Kind) : resource, bytes, CaptureMutation == "schema" ? 2 : 1,
                resource.Kind == NativeRecoveryArtifactKind.Database ? (CaptureMutation == "checksum" ? "invalid" : new string('A', 64)) :
                    CaptureMutation == "configuration-checksum" ? new string('A', 64) : null));
        }
        public ValueTask DisposeAsync() { Disposals++; Step("dispose"); return ValueTask.CompletedTask; }
        private void Step(string name)
        {
            Events.Add(name);
            if (name == CancelAt) { Cancellation.Cancel(); }
            if (name == FailAt) { throw new IOException("private-fixture-diagnostic"); }
        }
        public void Dispose() { Cancellation.Dispose(); CryptographicOperations.ZeroMemory(Passphrase); }
    }
}
