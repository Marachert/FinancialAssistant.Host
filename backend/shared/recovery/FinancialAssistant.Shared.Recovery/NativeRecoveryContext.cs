using System.Security.Cryptography;
using System.Text.Json;
using FinancialAssistant.Shared.Hosting;

namespace FinancialAssistant.Shared.Recovery;

public enum NativeRecoveryArtifactKind { Database = 1, ReceiptObject = 2, Configuration = 3, Manifest = 4 }

public sealed class NativeRecoveryContext
{
    private NativeRecoveryContext(Guid installationId, Guid backupId, Guid artifactId, string owner, NativeRecoveryArtifactKind kind)
    { SourceInstallationId = installationId; BackupId = backupId; ArtifactId = artifactId; Owner = owner; Kind = kind; }

    public Guid SourceInstallationId { get; }
    public Guid BackupId { get; }
    public Guid ArtifactId { get; }
    public string Owner { get; }
    public NativeRecoveryArtifactKind Kind { get; }

    public static NativeRecoveryContext Create(Guid installationId, Guid backupId, Guid artifactId, string owner, NativeRecoveryArtifactKind kind)
    {
        var host = NativeServiceCatalog.Services.SingleOrDefault(service => service.Id == owner);
        var validOwner = kind switch
        {
            NativeRecoveryArtifactKind.Database => owner == "financial-summary" || host?.RequiredSecretKeys.Any(key => key.EndsWith(":Storage:Postgres:Password", StringComparison.Ordinal)) == true,
            NativeRecoveryArtifactKind.ReceiptObject => owner == "receipt-processing",
            NativeRecoveryArtifactKind.Configuration => owner == "financial-summary" || host is not null,
            NativeRecoveryArtifactKind.Manifest => owner == "installation",
            _ => false
        };
        if (installationId == Guid.Empty || backupId == Guid.Empty || artifactId == Guid.Empty || !validOwner)
        { throw new ArgumentException("Native recovery artifact ownership or identity is invalid."); }
        return new(installationId, backupId, artifactId, owner, kind);
    }

    internal byte[] AdditionalData(ReadOnlySpan<byte> header)
    {
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            purpose = "FinancialAssistant.NativeRecovery.Artifact.v1",
            installation = SourceInstallationId.ToString("N"),
            backup = BackupId.ToString("N"),
            artifact = ArtifactId.ToString("N"),
            owner = Owner,
            kind = (int)Kind
        }));
        var result = new byte[header.Length + hash.Length]; header.CopyTo(result); hash.CopyTo(result, header.Length); return result;
    }
}
