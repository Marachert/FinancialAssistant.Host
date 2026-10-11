using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace FinancialAssistant.Shared.Persistence;

public sealed class RecordProtection : IDisposable
{
    public const int MaximumPayloadBytes = 1024 * 1024;
    private readonly byte[] indexKey;
    private readonly Dictionary<string, byte[]> keys;
    private readonly string currentKeyId;
    private bool disposed;

    public RecordProtection(byte[] indexKey, string currentKeyId, IReadOnlyDictionary<string, byte[]> keys)
    {
        if (indexKey.Length != 32 || keys.Count is < 1 or > 8 || !keys.ContainsKey(currentKeyId) ||
            keys.Any(pair => !ValidName(pair.Key) || pair.Key.Length > 32 || pair.Value.Length != 32 ||
                CryptographicOperations.FixedTimeEquals(indexKey, pair.Value)))
        {
            throw new ArgumentException("Storage requires distinct stable index/encryption keys and valid key IDs.");
        }

        this.indexKey = indexKey.ToArray();
        this.keys = keys.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
        this.currentKeyId = currentKeyId;
    }

    internal static bool ValidName(string value) => value.Length is >= 1 and <= 64 &&
        value.All(character => character is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

    internal IReadOnlyDictionary<string, byte[]> KeyMarkers(string serviceId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return keys.ToDictionary(pair => pair.Key, pair => HMACSHA256.HashData(pair.Value,
            Encoding.UTF8.GetBytes(serviceId + "|encryption-key|" + pair.Key)), StringComparer.Ordinal);
    }

    internal byte[] Index(params string[] parts)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (parts.Any(part => string.IsNullOrEmpty(part) || Encoding.UTF8.GetByteCount(part) > 4096))
        {
            throw new ArgumentException("Storage index input is missing or exceeds its bound.");
        }

        using var stream = new MemoryStream();
        Span<byte> length = stackalloc byte[4];
        foreach (var part in parts)
        {
            var bytes = Encoding.UTF8.GetBytes(part);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            stream.Write(length);
            stream.Write(bytes);
        }

        return HMACSHA256.HashData(indexKey, stream.ToArray());
    }

    internal byte[] Encrypt(byte[] payload, byte[] associatedData)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (payload.Length > MaximumPayloadBytes) { throw new ArgumentException("Storage payload exceeds its bound."); }
        var keyId = Encoding.ASCII.GetBytes(currentKeyId);
        var envelope = new byte[2 + keyId.Length + 12 + 16 + payload.Length];
        envelope[0] = 1;
        envelope[1] = (byte)keyId.Length;
        keyId.CopyTo(envelope, 2);
        var nonce = envelope.AsSpan(2 + keyId.Length, 12);
        RandomNumberGenerator.Fill(nonce);
        using var cipher = new AesGcm(keys[currentKeyId], 16);
        cipher.Encrypt(nonce, payload, envelope.AsSpan(30 + keyId.Length),
            envelope.AsSpan(14 + keyId.Length, 16), BindHeader(envelope.AsSpan(0, 2 + keyId.Length), associatedData));
        return envelope;
    }

    internal byte[] Decrypt(byte[] envelope, byte[] associatedData)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (envelope.Length < 31 || envelope[0] != 1 || envelope[1] is < 1 or > 32 ||
            envelope.Length < 30 + envelope[1] || envelope.Length > MaximumPayloadBytes + 62)
        {
            throw new StorageUnavailableException();
        }

        var keyId = Encoding.ASCII.GetString(envelope, 2, envelope[1]);
        if (!keys.TryGetValue(keyId, out var key)) { throw new StorageUnavailableException(); }
        var plaintext = new byte[envelope.Length - 30 - envelope[1]];
        if (plaintext.Length > MaximumPayloadBytes) { throw new StorageUnavailableException(); }
        try
        {
            using var cipher = new AesGcm(key, 16);
            cipher.Decrypt(envelope.AsSpan(2 + envelope[1], 12), envelope.AsSpan(30 + envelope[1]),
                envelope.AsSpan(14 + envelope[1], 16), plaintext, BindHeader(envelope.AsSpan(0, 2 + envelope[1]), associatedData));
            return plaintext;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new StorageUnavailableException();
        }
    }

    private static byte[] BindHeader(ReadOnlySpan<byte> header, byte[] associatedData)
    {
        var bound = new byte[header.Length + associatedData.Length];
        header.CopyTo(bound);
        associatedData.CopyTo(bound, header.Length);
        return bound;
    }

    public void Dispose()
    {
        if (disposed) { return; }
        disposed = true;
        CryptographicOperations.ZeroMemory(indexKey);
        foreach (var key in keys.Values) { CryptographicOperations.ZeroMemory(key); }
    }
}
