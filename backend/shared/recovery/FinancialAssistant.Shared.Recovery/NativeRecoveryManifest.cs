using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinancialAssistant.Shared.Hosting;

namespace FinancialAssistant.Shared.Recovery;

public enum NativeRecoveryBrokerPolicy { DrainedThenRecreate = 1 }
public enum NativeRecoverySearchPolicy { RebuildFromOwnedState = 1 }
public sealed record NativeRecoveryResource(string Owner, NativeRecoveryArtifactKind Kind);

public sealed record NativeRecoveryArtifactDescriptor
{
    public Guid Id { get; init; }
    public string Owner { get; init; } = "";
    public NativeRecoveryArtifactKind Kind { get; init; }
    public int CiphertextBytes { get; init; }
    public string Sha256 { get; init; } = "";
    public int DataSchemaVersion { get; init; }
    public string? MigrationChecksum { get; init; }
}

public sealed class NativeRecoveryManifestDocument
{
    public int SchemaVersion { get; init; }
    public Guid SourceInstallationId { get; init; }
    public Guid BackupId { get; init; }
    public Guid ManifestId { get; init; }
    public Guid ConsistencyBoundaryId { get; init; }
    public DateTimeOffset CapturedAtUtc { get; init; }
    public string ApplicationVersion { get; init; } = "";
    public string InstallerManifestSha256 { get; init; } = "";
    public int PostgresMajorVersion { get; init; }
    public NativeRecoveryBrokerPolicy BrokerPolicy { get; init; }
    public NativeRecoverySearchPolicy SearchPolicy { get; init; }
    public List<NativeRecoveryArtifactDescriptor> Artifacts { get; init; } = new();
}

public sealed class NativeRecoveryManifest
{
    public const int MaximumDocumentBytes = 65536;
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };
    private readonly byte[] document;

    public static IReadOnlyList<NativeRecoveryResource> RequiredResources { get; } = Array.AsReadOnly(
        NativeServiceCatalog.Services.Where(service => service.RequiredSecretKeys.Any(key =>
                key.EndsWith(":Storage:Postgres:Password", StringComparison.Ordinal)))
            .Select(service => new NativeRecoveryResource(service.Id, NativeRecoveryArtifactKind.Database))
            .Append(new("financial-summary", NativeRecoveryArtifactKind.Database))
            .Concat(NativeServiceCatalog.Services.Select(service => new NativeRecoveryResource(service.Id, NativeRecoveryArtifactKind.Configuration)))
            .Append(new("financial-summary", NativeRecoveryArtifactKind.Configuration))
            .Append(new("receipt-processing", NativeRecoveryArtifactKind.ReceiptObject))
            .OrderBy(resource => resource.Owner, StringComparer.Ordinal).ThenBy(resource => resource.Kind).ToArray());

    private NativeRecoveryManifest(NativeRecoveryManifestDocument model, byte[] document)
    {
        SourceInstallationId = model.SourceInstallationId; BackupId = model.BackupId; ManifestId = model.ManifestId;
        ConsistencyBoundaryId = model.ConsistencyBoundaryId; CapturedAtUtc = model.CapturedAtUtc;
        ApplicationVersion = model.ApplicationVersion; InstallerManifestSha256 = model.InstallerManifestSha256;
        this.document = document;
        Artifacts = Array.AsReadOnly(model.Artifacts.OrderBy(artifact => artifact.Owner, StringComparer.Ordinal).ThenBy(artifact => artifact.Kind).ToArray());
    }

    public Guid SourceInstallationId { get; }
    public Guid BackupId { get; }
    public Guid ManifestId { get; }
    public Guid ConsistencyBoundaryId { get; }
    public DateTimeOffset CapturedAtUtc { get; }
    public string ApplicationVersion { get; }
    public string InstallerManifestSha256 { get; }
    public IReadOnlyList<NativeRecoveryArtifactDescriptor> Artifacts { get; }
    public byte[] ExportDocument() => document.ToArray();

    public static NativeRecoveryManifest Create(NativeRecoveryManifestDocument model)
    {
        try { return Parse(JsonSerializer.SerializeToUtf8Bytes(model, Json)); }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException or InvalidOperationException)
        { throw Invalid(); }
    }

    public byte[] Seal(ReadOnlySpan<byte> recoveryKey) => NativeRecoveryArtifactEncryption.Seal(document, recoveryKey,
        NativeRecoveryContext.Create(SourceInstallationId, BackupId, ManifestId, "installation", NativeRecoveryArtifactKind.Manifest));

    public static NativeRecoveryManifest Open(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> recoveryKey, NativeRecoveryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Owner != "installation" || context.Kind != NativeRecoveryArtifactKind.Manifest ||
            ciphertext.Length > MaximumDocumentBytes + NativeRecoveryArtifactEncryption.EnvelopeOverheadBytes) { throw Invalid(); }
        var plaintext = NativeRecoveryArtifactEncryption.Open(ciphertext, recoveryKey, context);
        try
        {
            var result = Parse(plaintext);
            if (result.SourceInstallationId != context.SourceInstallationId || result.BackupId != context.BackupId || result.ManifestId != context.ArtifactId)
            { throw Invalid(); }
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    public byte[] OpenArtifact(Guid artifactId, ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> recoveryKey)
    {
        var artifact = Artifacts.SingleOrDefault(value => value.Id == artifactId);
        if (artifact is null || ciphertext.Length != artifact.CiphertextBytes ||
            !CryptographicOperations.FixedTimeEquals(SHA256.HashData(ciphertext), Convert.FromHexString(artifact.Sha256))) { throw Invalid(); }
        return NativeRecoveryArtifactEncryption.Open(ciphertext, recoveryKey,
            NativeRecoveryContext.Create(SourceInstallationId, BackupId, artifact.Id, artifact.Owner, artifact.Kind));
    }

    private static NativeRecoveryManifest Parse(ReadOnlySpan<byte> utf8)
    {
        try
        {
            if (utf8.Length is < 2 or > MaximumDocumentBytes) { throw Invalid(); }
            using var parsed = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions { MaxDepth = 8 });
            RejectDuplicates(parsed.RootElement);
            var model = JsonSerializer.Deserialize<NativeRecoveryManifestDocument>(utf8, Json) ?? throw Invalid();
            if (model.SchemaVersion != 1 || model.SourceInstallationId == Guid.Empty || model.BackupId == Guid.Empty ||
                model.ManifestId == Guid.Empty || model.ConsistencyBoundaryId == Guid.Empty || model.CapturedAtUtc < DateTimeOffset.UnixEpoch ||
                model.CapturedAtUtc.Offset != TimeSpan.Zero || model.ApplicationVersion is null || model.ApplicationVersion.Length > 32 ||
                !Version.TryParse(model.ApplicationVersion, out var version) || version.Major < 1 || version.ToString() != model.ApplicationVersion ||
                !Digest(model.InstallerManifestSha256) || model.PostgresMajorVersion != 18 ||
                model.BrokerPolicy != NativeRecoveryBrokerPolicy.DrainedThenRecreate || model.SearchPolicy != NativeRecoverySearchPolicy.RebuildFromOwnedState ||
                model.Artifacts is null || model.Artifacts.Count != RequiredResources.Count) { throw Invalid(); }
            var ids = new HashSet<Guid>();
            var resources = new HashSet<NativeRecoveryResource>();
            foreach (var artifact in model.Artifacts)
            {
                if (artifact is null || artifact.Id == Guid.Empty || artifact.Id == model.ManifestId || !ids.Add(artifact.Id) ||
                    !resources.Add(new(artifact.Owner, artifact.Kind)) || artifact.DataSchemaVersion != 1 ||
                    artifact.CiphertextBytes is <= NativeRecoveryArtifactEncryption.EnvelopeOverheadBytes or > NativeRecoveryArtifactEncryption.MaximumPlaintextBytes + NativeRecoveryArtifactEncryption.EnvelopeOverheadBytes ||
                    !Digest(artifact.Sha256) || (artifact.Kind == NativeRecoveryArtifactKind.Database ? !Digest(artifact.MigrationChecksum) : artifact.MigrationChecksum is not null))
                { throw Invalid(); }
                _ = NativeRecoveryContext.Create(model.SourceInstallationId, model.BackupId, artifact.Id, artifact.Owner, artifact.Kind);
            }
            if (!resources.SetEquals(RequiredResources)) { throw Invalid(); }
            var canonical = JsonSerializer.SerializeToUtf8Bytes(model, Json);
            if (canonical.Length > MaximumDocumentBytes) { throw Invalid(); }
            return new(model, canonical);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or NotSupportedException or InvalidOperationException)
        { throw Invalid(); }
    }

    private static bool Digest(string? value) => value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'A' and <= 'F');

    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject()) { if (!names.Add(property.Name)) { throw Invalid(); } RejectDuplicates(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) { foreach (var item in element.EnumerateArray()) { RejectDuplicates(item); } }
    }

    private static InvalidOperationException Invalid() => new("Native recovery manifest or artifact inventory is invalid.");
}
