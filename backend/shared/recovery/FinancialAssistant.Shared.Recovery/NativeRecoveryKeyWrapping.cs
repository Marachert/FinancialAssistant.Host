using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;

namespace FinancialAssistant.Shared.Recovery;

public static class NativeRecoveryKeyWrapping
{
    public const int Iterations = 600000;
    public const int EnvelopeBytes = 104;
    private static ReadOnlySpan<byte> Magic => "FAKEYW01"u8;

    public static byte[] CreateRecoveryKey() => RandomNumberGenerator.GetBytes(32);

    public static byte[] Wrap(Guid sourceInstallationId, Guid backupId, ReadOnlySpan<byte> recoveryKey, ReadOnlySpan<byte> operatorPassphrase)
    {
        Validate(sourceInstallationId, backupId, operatorPassphrase);
        if (recoveryKey.Length != 32) { throw Invalid(); }
        var envelope = new byte[EnvelopeBytes]; Magic.CopyTo(envelope);
        BinaryPrimitives.WriteInt32BigEndian(envelope.AsSpan(8, 4), Iterations);
        RandomNumberGenerator.Fill(envelope.AsSpan(12, 44));
        Span<byte> wrappingKey = stackalloc byte[32];
        try
        {
            Rfc2898DeriveBytes.Pbkdf2(operatorPassphrase, envelope.AsSpan(12, 32), wrappingKey, Iterations, HashAlgorithmName.SHA256);
            using var cipher = new AesGcm(wrappingKey, 16);
            cipher.Encrypt(envelope.AsSpan(44, 12), recoveryKey, envelope.AsSpan(72, 32), envelope.AsSpan(56, 16),
                AdditionalData(sourceInstallationId, backupId, envelope.AsSpan(0, 56)));
            return envelope;
        }
        finally { CryptographicOperations.ZeroMemory(wrappingKey); }
    }

    public static byte[] Unwrap(Guid sourceInstallationId, Guid backupId, ReadOnlySpan<byte> envelope, ReadOnlySpan<byte> operatorPassphrase)
    {
        Validate(sourceInstallationId, backupId, operatorPassphrase);
        // An untrusted envelope cannot select an expensive KDF work factor or downgrade this version.
        if (envelope.Length != EnvelopeBytes || !envelope[..8].SequenceEqual(Magic) ||
            BinaryPrimitives.ReadInt32BigEndian(envelope.Slice(8, 4)) != Iterations) { throw Invalid(); }
        Span<byte> wrappingKey = stackalloc byte[32];
        var recoveryKey = new byte[32];
        try
        {
            Rfc2898DeriveBytes.Pbkdf2(operatorPassphrase, envelope.Slice(12, 32), wrappingKey, Iterations, HashAlgorithmName.SHA256);
            using var cipher = new AesGcm(wrappingKey, 16);
            cipher.Decrypt(envelope.Slice(44, 12), envelope.Slice(72, 32), envelope.Slice(56, 16), recoveryKey,
                AdditionalData(sourceInstallationId, backupId, envelope[..56]));
            return recoveryKey;
        }
        catch (CryptographicException) { CryptographicOperations.ZeroMemory(recoveryKey); throw Invalid(); }
        finally { CryptographicOperations.ZeroMemory(wrappingKey); }
    }

    private static void Validate(Guid installationId, Guid backupId, ReadOnlySpan<byte> passphrase)
    { if (installationId == Guid.Empty || backupId == Guid.Empty || passphrase.Length is < 32 or > 1024) { throw Invalid(); } }

    private static byte[] AdditionalData(Guid installationId, Guid backupId, ReadOnlySpan<byte> header)
    {
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { purpose = "FinancialAssistant.NativeRecovery.OperatorKey.v1", installation = installationId.ToString("N"), backup = backupId.ToString("N") }));
        var result = new byte[header.Length + hash.Length]; header.CopyTo(result); hash.CopyTo(result, header.Length); return result;
    }

    private static InvalidOperationException Invalid() => new("Native recovery key envelope or operator unlock material is invalid.");
}
