using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Shared.Recovery;
using Xunit;

namespace FinancialAssistant.Shared.Recovery.Tests;

public sealed class NativeRecoveryEncryptionTests
{
    [Theory]
    [InlineData("income", NativeRecoveryArtifactKind.Database)]
    [InlineData("financial-summary", NativeRecoveryArtifactKind.Database)]
    [InlineData("receipt-processing", NativeRecoveryArtifactKind.ReceiptObject)]
    [InlineData("gateway", NativeRecoveryArtifactKind.Configuration)]
    [InlineData("financial-summary", NativeRecoveryArtifactKind.Configuration)]
    [InlineData("installation", NativeRecoveryArtifactKind.Manifest)]
    public void AuthenticatedOwnedArtifactRoundtripIsRandomized(string owner, NativeRecoveryArtifactKind kind)
    {
        var context = NativeRecoveryContext.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), owner, kind);
        var key = NativeRecoveryKeyWrapping.CreateRecoveryKey(); var input = Encoding.UTF8.GetBytes("synthetic-financial-backup-input");
        byte[]? output = null;
        try
        {
            var envelope = NativeRecoveryArtifactEncryption.Seal(input, key, context);
            Assert.NotEqual(envelope, NativeRecoveryArtifactEncryption.Seal(input, key, context));
            Assert.DoesNotContain("synthetic-financial", Encoding.UTF8.GetString(envelope));
            output = NativeRecoveryArtifactEncryption.Open(envelope, key, context); Assert.Equal(input, output);
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(input); if (output is not null) { CryptographicOperations.ZeroMemory(output); } }
    }

    [Theory]
    [InlineData("gateway", NativeRecoveryArtifactKind.Database)]
    [InlineData("income", NativeRecoveryArtifactKind.ReceiptObject)]
    [InlineData("foreign", NativeRecoveryArtifactKind.Configuration)]
    [InlineData("income", NativeRecoveryArtifactKind.Manifest)]
    [InlineData("income", (NativeRecoveryArtifactKind)99)]
    public void ForeignOrMismatchedResourceOwnershipCannotCreateAContext(string owner, NativeRecoveryArtifactKind kind) =>
        Assert.Throws<ArgumentException>(() => NativeRecoveryContext.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), owner, kind));

    [Theory]
    [InlineData("installation")]
    [InlineData("backup")]
    [InlineData("artifact")]
    [InlineData("owner")]
    [InlineData("purpose")]
    [InlineData("magic")]
    [InlineData("nonce")]
    [InlineData("tag")]
    [InlineData("ciphertext")]
    public void ArtifactCannotBeReplayedOrAltered(string mutation)
    {
        var original = NativeRecoveryContext.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "income", NativeRecoveryArtifactKind.Database);
        var key = NativeRecoveryKeyWrapping.CreateRecoveryKey();
        try
        {
            var envelope = NativeRecoveryArtifactEncryption.Seal("synthetic-private-payload"u8, key, original);
            var altered = NativeRecoveryContext.Create(mutation == "installation" ? Guid.NewGuid() : original.SourceInstallationId,
                mutation == "backup" ? Guid.NewGuid() : original.BackupId, mutation == "artifact" ? Guid.NewGuid() : original.ArtifactId,
                mutation == "owner" ? "expense" : original.Owner, mutation == "purpose" ? NativeRecoveryArtifactKind.Configuration : original.Kind);
            var offset = mutation switch { "magic" => 0, "nonce" => 8, "tag" => 20, "ciphertext" => 36, _ => -1 };
            if (offset >= 0) { envelope[offset] ^= 1; }
            var error = Assert.Throws<InvalidOperationException>(() => NativeRecoveryArtifactEncryption.Open(envelope, key, altered));
            Assert.Null(error.InnerException); Assert.DoesNotContain("synthetic-private", error.ToString());
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("installation")]
    [InlineData("backup")]
    [InlineData("passphrase")]
    [InlineData("magic")]
    [InlineData("iterations")]
    [InlineData("salt")]
    [InlineData("nonce")]
    [InlineData("tag")]
    [InlineData("ciphertext")]
    public void OperatorWrappedKeyIsPortableButBoundToBackupAndCannotBeTampered(string mutation)
    {
        var installation = Guid.NewGuid(); var backup = Guid.NewGuid();
        var key = NativeRecoveryKeyWrapping.CreateRecoveryKey(); var passphrase = Encoding.UTF8.GetBytes("synthetic-only-portable-recovery-operator-passphrase");
        byte[]? output = null;
        try
        {
            var envelope = NativeRecoveryKeyWrapping.Wrap(installation, backup, key, passphrase);
            var offset = mutation switch { "magic" => 0, "iterations" => 8, "salt" => 12, "nonce" => 44, "tag" => 56, "ciphertext" => 72, _ => -1 };
            if (offset >= 0) { envelope[offset] ^= 1; }
            if (mutation == "passphrase") { passphrase[0] ^= 1; }
            if (mutation == "valid")
            { output = NativeRecoveryKeyWrapping.Unwrap(installation, backup, envelope, passphrase); Assert.Equal(key, output); }
            else
            {
                var error = Assert.Throws<InvalidOperationException>(() => NativeRecoveryKeyWrapping.Unwrap(
                    mutation == "installation" ? Guid.NewGuid() : installation, mutation == "backup" ? Guid.NewGuid() : backup, envelope, passphrase));
                Assert.Null(error.InnerException); Assert.DoesNotContain("synthetic", error.ToString());
            }
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(passphrase); if (output is not null) { CryptographicOperations.ZeroMemory(output); } }
    }

    [Fact]
    public void InvalidBoundsAndEmptyIdentityFailBeforeAnyArtifactIsOpened()
    {
        var context = NativeRecoveryContext.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "income", NativeRecoveryArtifactKind.Database);
        Assert.Throws<ArgumentException>(() => NativeRecoveryContext.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "income", NativeRecoveryArtifactKind.Database));
        Assert.Throws<InvalidOperationException>(() => NativeRecoveryArtifactEncryption.Seal(Array.Empty<byte>(), new byte[32], context));
        Assert.Throws<InvalidOperationException>(() => NativeRecoveryArtifactEncryption.Open(new byte[36], new byte[32], context));
        Assert.Throws<InvalidOperationException>(() => NativeRecoveryArtifactEncryption.Seal(new byte[1], new byte[31], context));
        Assert.Throws<InvalidOperationException>(() => NativeRecoveryKeyWrapping.Wrap(Guid.NewGuid(), Guid.NewGuid(), new byte[32], new byte[31]));
        Assert.Throws<InvalidOperationException>(() => NativeRecoveryKeyWrapping.Unwrap(Guid.NewGuid(), Guid.NewGuid(), new byte[105], new byte[32]));
    }
}
