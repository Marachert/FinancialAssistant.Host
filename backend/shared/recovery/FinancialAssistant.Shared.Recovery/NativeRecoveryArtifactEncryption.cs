using System.Security.Cryptography;

namespace FinancialAssistant.Shared.Recovery;

public static class NativeRecoveryArtifactEncryption
{
    public const int MaximumPlaintextBytes = 64 * 1024 * 1024;
    public const int EnvelopeOverheadBytes = 36;
    private static ReadOnlySpan<byte> Magic => "FAARTF01"u8;

    public static byte[] Seal(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> recoveryKey, NativeRecoveryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (recoveryKey.Length != 32 || plaintext.Length is < 1 or > MaximumPlaintextBytes) { throw Invalid(); }
        var envelope = new byte[EnvelopeOverheadBytes + plaintext.Length]; Magic.CopyTo(envelope);
        RandomNumberGenerator.Fill(envelope.AsSpan(8, 12));
        using var cipher = new AesGcm(recoveryKey, 16);
        cipher.Encrypt(envelope.AsSpan(8, 12), plaintext, envelope.AsSpan(36), envelope.AsSpan(20, 16), context.AdditionalData(envelope.AsSpan(0, 20)));
        return envelope;
    }

    public static byte[] Open(ReadOnlySpan<byte> envelope, ReadOnlySpan<byte> recoveryKey, NativeRecoveryContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (recoveryKey.Length != 32 || envelope.Length is <= EnvelopeOverheadBytes or > MaximumPlaintextBytes + EnvelopeOverheadBytes ||
            !envelope[..8].SequenceEqual(Magic)) { throw Invalid(); }
        var plaintext = new byte[envelope.Length - EnvelopeOverheadBytes];
        try
        {
            using var cipher = new AesGcm(recoveryKey, 16);
            cipher.Decrypt(envelope.Slice(8, 12), envelope[36..], envelope.Slice(20, 16), plaintext, context.AdditionalData(envelope[..20]));
            return plaintext;
        }
        catch (CryptographicException) { CryptographicOperations.ZeroMemory(plaintext); throw Invalid(); }
    }

    private static InvalidOperationException Invalid() => new("Native recovery artifact, key or authenticated context is invalid.");
}
