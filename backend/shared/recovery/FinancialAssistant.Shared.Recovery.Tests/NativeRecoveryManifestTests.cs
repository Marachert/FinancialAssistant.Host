using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FinancialAssistant.Shared.Hosting;
using Xunit;

namespace FinancialAssistant.Shared.Recovery.Tests;

public sealed class NativeRecoveryManifestTests
{
    [Fact]
    public void InventoryCoversAllDatabaseAndConfigurationOwnersIncludingSummaryAndReceipts()
    {
        var resources = NativeRecoveryManifest.RequiredResources;
        Assert.Equal(30, resources.Count);
        Assert.Equal(13, resources.Count(resource => resource.Kind == NativeRecoveryArtifactKind.Database));
        Assert.Equal(16, resources.Count(resource => resource.Kind == NativeRecoveryArtifactKind.Configuration));
        Assert.Equal(NativeServiceCatalog.Services.Count + 1, resources.Count(resource => resource.Kind == NativeRecoveryArtifactKind.Configuration));
        Assert.Contains(new("financial-summary", NativeRecoveryArtifactKind.Database), resources);
        Assert.Contains(new("financial-summary", NativeRecoveryArtifactKind.Configuration), resources);
        Assert.Equal(new("receipt-processing", NativeRecoveryArtifactKind.ReceiptObject), Assert.Single(resources, resource => resource.Kind == NativeRecoveryArtifactKind.ReceiptObject));
        Assert.DoesNotContain(resources, resource => resource.Owner is "gateway" or "mcp" && resource.Kind == NativeRecoveryArtifactKind.Database);
    }

    [Fact]
    public void AuthenticatedManifestOwnsImmutableCopiesAndOpensEveryBoundArtifact()
    {
        using var fixture = new Fixture();
        var created = NativeRecoveryManifest.Create(fixture.Document);
        var envelope = created.Seal(fixture.Key);
        var restored = NativeRecoveryManifest.Open(envelope, fixture.Key, fixture.Context);
        fixture.Document.Artifacts.Clear();
        created.ExportDocument()[0] = 0;
        Assert.Equal(30, created.Artifacts.Count);
        Assert.Equal(created.SourceInstallationId, restored.SourceInstallationId);
        Assert.Equal(created.BackupId, restored.BackupId);
        Assert.Equal(created.ConsistencyBoundaryId, restored.ConsistencyBoundaryId);
        Assert.Equal(created.CapturedAtUtc, restored.CapturedAtUtc);
        Assert.Equal("1.0.0", restored.ApplicationVersion);
        Assert.Equal(created.InstallerManifestSha256, restored.InstallerManifestSha256);
        Assert.Throws<NotSupportedException>(() => ((ICollection<NativeRecoveryArtifactDescriptor>)restored.Artifacts).Clear());
        foreach (var artifact in restored.Artifacts)
        {
            var plaintext = restored.OpenArtifact(artifact.Id, fixture.Payloads[artifact.Id], fixture.Key);
            try { Assert.Equal("synthetic-owned-recovery-payload", Encoding.UTF8.GetString(plaintext)); }
            finally { CryptographicOperations.ZeroMemory(plaintext); }
        }
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("source")]
    [InlineData("backup")]
    [InlineData("manifest")]
    [InlineData("source-context-mismatch")]
    [InlineData("backup-context-mismatch")]
    [InlineData("manifest-context-mismatch")]
    [InlineData("boundary")]
    [InlineData("time")]
    [InlineData("offset")]
    [InlineData("application")]
    [InlineData("installer-digest")]
    [InlineData("postgres")]
    [InlineData("broker")]
    [InlineData("search")]
    [InlineData("missing-owner")]
    [InlineData("partial-two-owner-drill")]
    [InlineData("extra-owner")]
    [InlineData("duplicate-owner")]
    [InlineData("duplicate-id")]
    [InlineData("manifest-id-collision")]
    [InlineData("foreign-owner")]
    [InlineData("null-owner")]
    [InlineData("kind")]
    [InlineData("data-version")]
    [InlineData("too-small")]
    [InlineData("too-large")]
    [InlineData("digest")]
    [InlineData("migration")]
    [InlineData("configuration-migration")]
    [InlineData("null-artifacts")]
    [InlineData("null-artifact")]
    [InlineData("unknown-property")]
    public void IncompleteOrInvalidAuthenticatedInventoryIsRejected(string mutation)
    {
        using var fixture = new Fixture();
        var root = JsonNode.Parse(NativeRecoveryManifest.Create(fixture.Document).ExportDocument())!.AsObject();
        var artifacts = root["artifacts"]!.AsArray(); var first = artifacts[0]!.AsObject();
        switch (mutation)
        {
            case "schema": root["schemaVersion"] = 2; break;
            case "source": root["sourceInstallationId"] = Guid.Empty; break;
            case "backup": root["backupId"] = Guid.Empty; break;
            case "manifest": root["manifestId"] = Guid.Empty; break;
            case "source-context-mismatch": root["sourceInstallationId"] = Guid.NewGuid(); break;
            case "backup-context-mismatch": root["backupId"] = Guid.NewGuid(); break;
            case "manifest-context-mismatch": root["manifestId"] = Guid.NewGuid(); break;
            case "boundary": root["consistencyBoundaryId"] = Guid.Empty; break;
            case "time": root["capturedAtUtc"] = DateTimeOffset.MinValue; break;
            case "offset": root["capturedAtUtc"] = fixture.Document.CapturedAtUtc.ToOffset(TimeSpan.FromHours(1)); break;
            case "application": root["applicationVersion"] = "untrusted-path-or-command"; break;
            case "installer-digest": root["installerManifestSha256"] = "missing"; break;
            case "postgres": root["postgresMajorVersion"] = 19; break;
            case "broker": root["brokerPolicy"] = 0; break;
            case "search": root["searchPolicy"] = 0; break;
            case "missing-owner": artifacts.RemoveAt(0); break;
            case "partial-two-owner-drill": while (artifacts.Count > 4) { artifacts.RemoveAt(0); } break;
            case "extra-owner": artifacts.Add(first.DeepClone()); break;
            case "duplicate-owner": artifacts[1]!["owner"] = first["owner"]!.DeepClone(); artifacts[1]!["kind"] = first["kind"]!.DeepClone(); break;
            case "duplicate-id": artifacts[1]!["id"] = first["id"]!.DeepClone(); break;
            case "manifest-id-collision": first["id"] = root["manifestId"]!.DeepClone(); break;
            case "foreign-owner": first["owner"] = "foreign"; break;
            case "null-owner": first["owner"] = null; break;
            case "kind": first["kind"] = 99; break;
            case "data-version": first["dataSchemaVersion"] = 2; break;
            case "too-small": first["ciphertextBytes"] = 36; break;
            case "too-large": first["ciphertextBytes"] = NativeRecoveryArtifactEncryption.MaximumPlaintextBytes + 37; break;
            case "digest": first["sha256"] = new string('z', 64); break;
            case "migration": artifacts.First(value => value!["kind"]!.GetValue<int>() == 1)!["migrationChecksum"] = null; break;
            case "configuration-migration": artifacts.First(value => value!["kind"]!.GetValue<int>() == 3)!["migrationChecksum"] = new string('A', 64); break;
            case "null-artifacts": root["artifacts"] = null; break;
            case "null-artifact": artifacts[0] = null; break;
            case "unknown-property": root["paidFallback"] = true; break;
            default: throw new InvalidOperationException();
        }
        var authenticated = NativeRecoveryArtifactEncryption.Seal(Encoding.UTF8.GetBytes(root.ToJsonString()), fixture.Key, fixture.Context);
        var error = Assert.Throws<InvalidOperationException>(() => NativeRecoveryManifest.Open(authenticated, fixture.Key, fixture.Context));
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("synthetic-owned", error.ToString());
    }

    [Theory]
    [InlineData("duplicate-root")]
    [InlineData("case-duplicate")]
    [InlineData("duplicate-nested")]
    [InlineData("oversized")]
    public void AmbiguousOrOversizedJsonIsRejectedEvenWhenAuthenticated(string mutation)
    {
        using var fixture = new Fixture();
        var json = Encoding.UTF8.GetString(NativeRecoveryManifest.Create(fixture.Document).ExportDocument());
        json = mutation switch
        {
            "duplicate-root" => json.Insert(1, "\"schemaVersion\":1,"),
            "case-duplicate" => json.Insert(1, "\"SchemaVersion\":1,"),
            "duplicate-nested" => json.Replace("\"ciphertextBytes\":", "\"ciphertextBytes\":37,\"ciphertextBytes\":", StringComparison.Ordinal),
            "oversized" => json + new string(' ', NativeRecoveryManifest.MaximumDocumentBytes),
            _ => throw new InvalidOperationException()
        };
        var envelope = NativeRecoveryArtifactEncryption.Seal(Encoding.UTF8.GetBytes(json), fixture.Key, fixture.Context);
        Assert.Throws<InvalidOperationException>(() => NativeRecoveryManifest.Open(envelope, fixture.Key, fixture.Context));
    }

    [Theory]
    [InlineData("manifest-context")]
    [InlineData("wrong-key")]
    [InlineData("artifact-id")]
    [InlineData("ciphertext")]
    [InlineData("truncated")]
    public void ManifestOrArtifactReplacementCannotBeOpened(string mutation)
    {
        using var fixture = new Fixture(); var manifest = NativeRecoveryManifest.Create(fixture.Document);
        var artifact = manifest.Artifacts[0]; var bytes = fixture.Payloads[artifact.Id].ToArray();
        var key = fixture.Key.ToArray();
        try
        {
            if (mutation == "manifest-context")
            {
                var context = NativeRecoveryContext.Create(Guid.NewGuid(), fixture.Document.BackupId, fixture.Document.ManifestId, "installation", NativeRecoveryArtifactKind.Manifest);
                Assert.Throws<InvalidOperationException>(() => NativeRecoveryManifest.Open(manifest.Seal(key), key, context)); return;
            }
            if (mutation == "wrong-key") { key[0] ^= 1; }
            if (mutation == "ciphertext") { bytes[^1] ^= 1; }
            if (mutation == "truncated") { bytes = bytes[..^1]; }
            Assert.Throws<InvalidOperationException>(() => manifest.OpenArtifact(mutation == "artifact-id" ? Guid.NewGuid() : artifact.Id, bytes, key));
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private sealed class Fixture : IDisposable
    {
        public byte[] Key { get; } = NativeRecoveryKeyWrapping.CreateRecoveryKey();
        public Dictionary<Guid, byte[]> Payloads { get; } = new();
        public NativeRecoveryManifestDocument Document { get; }
        public NativeRecoveryContext Context => NativeRecoveryContext.Create(Document.SourceInstallationId, Document.BackupId, Document.ManifestId, "installation", NativeRecoveryArtifactKind.Manifest);

        public Fixture()
        {
            var installation = Guid.NewGuid(); var backup = Guid.NewGuid();
            var artifacts = new List<NativeRecoveryArtifactDescriptor>();
            foreach (var resource in NativeRecoveryManifest.RequiredResources)
            {
                var id = Guid.NewGuid();
                var bytes = NativeRecoveryArtifactEncryption.Seal("synthetic-owned-recovery-payload"u8, Key,
                    NativeRecoveryContext.Create(installation, backup, id, resource.Owner, resource.Kind));
                Payloads.Add(id, bytes);
                artifacts.Add(new()
                {
                    Id = id,
                    Owner = resource.Owner,
                    Kind = resource.Kind,
                    CiphertextBytes = bytes.Length,
                    Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
                    DataSchemaVersion = 1,
                    MigrationChecksum = resource.Kind == NativeRecoveryArtifactKind.Database ? new string('A', 64) : null
                });
            }
            Document = new()
            {
                SchemaVersion = 1,
                SourceInstallationId = installation,
                BackupId = backup,
                ManifestId = Guid.NewGuid(),
                ConsistencyBoundaryId = Guid.NewGuid(),
                CapturedAtUtc = DateTimeOffset.Parse("2026-11-03T08:00:00Z"),
                ApplicationVersion = "1.0.0",
                InstallerManifestSha256 = new string('B', 64),
                PostgresMajorVersion = 18,
                BrokerPolicy = NativeRecoveryBrokerPolicy.DrainedThenRecreate,
                SearchPolicy = NativeRecoverySearchPolicy.RebuildFromOwnedState,
                Artifacts = artifacts
            };
        }

        public void Dispose() => CryptographicOperations.ZeroMemory(Key);
    }
}
