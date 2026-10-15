using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FinancialAssistant.ReceiptProcessing.Application;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.Shared.NativeFiles;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

public sealed class NativeReceiptObjectStore : IReceiptObjectStore, IDisposable
{
    private const int MaximumEnvelope = (int)ReceiptProcessingService.MaximumReceiptSizeBytes + 1024;
    private static readonly string MarkerName = Convert.ToHexString(SHA256.HashData("financial-assistant-receipt-key-marker-v1"u8)) + ".object";
    private readonly ProtectedDirectory files;
    private readonly NativeReceiptObjectOptions options;
    private readonly TimeProvider clock;
    private readonly byte[] indexKey;
    private readonly Dictionary<string, byte[]> keys;
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool disposed;
    private string? cleanupAfter;
    private int retentionHours;

    public NativeReceiptObjectStore(NativeReceiptObjectOptions options, TimeProvider clock)
    {
        options.Validate();
        this.options = options;
        this.clock = clock;
        retentionHours = options.RetentionHours;
        files = new ProtectedDirectory(options.RootPath, options.ServiceSid, options.MaximumObjects + 32);
        indexKey = Convert.FromBase64String(options.IndexKey);
        keys = options.EncryptionKeys.ToDictionary(pair => pair.Key, pair => Convert.FromBase64String(pair.Value), StringComparer.Ordinal);
        try { ValidateKeyMarker(); }
        catch
        {
            files.Dispose(); CryptographicOperations.ZeroMemory(indexKey);
            foreach (var key in keys.Values) { CryptographicOperations.ZeroMemory(key); }
            throw new NativeReceiptStorageException();
        }
    }

    public Task StoreAsync(string receiptId, ReadOnlyMemory<byte> content, CancellationToken token)
    {
        ValidateId(receiptId);
        if (content.Length is < 1 or > (int)ReceiptProcessingService.MaximumReceiptSizeBytes) { throw new ArgumentException("Invalid receipt object size."); }
        return LockedAsync(async () =>
        {
            var name = Name(receiptId);
            var existing = await LoadAsync(name, receiptId, token);
            var digest = SHA256.HashData(content.Span);
            if (existing is not null)
            {
                try
                {
                    if (existing.Deleted || Expired(existing) || existing.Size != content.Length || !CryptographicOperations.FixedTimeEquals(existing.Digest, digest))
                    { throw new NativeReceiptStorageException(); }
                    return true;
                }
                finally { existing.Clear(); }
            }
            if (files.ListFiles().Count(name => name != MarkerName && name.EndsWith(".object", StringComparison.Ordinal)) >= options.MaximumObjects)
            { throw new NativeReceiptStorageException(); }
            var now = clock.GetUtcNow().ToUniversalTime();
            var state = new ObjectState(receiptId, now, now.AddHours(retentionHours), content.Length, digest, content.ToArray(), false);
            try { await SaveAsync(name, state, replace: false, token); }
            finally { state.Clear(); }
            return true;
        }, token);
    }

    public Task<Stream?> OpenReadAsync(string receiptId, CancellationToken token)
    {
        ValidateId(receiptId);
        return LockedAsync<Stream?>(async () =>
        {
            var state = await LoadAsync(Name(receiptId), receiptId, token);
            if (state is null) { return null; }
            if (state.Deleted || Expired(state)) { state.Clear(); return null; }
            CryptographicOperations.ZeroMemory(state.Digest);
            return new ZeroingStream(state.Content);
        }, token);
    }

    public Task<NativeReceiptObjectInfo?> GetInfoAsync(string receiptId, CancellationToken token = default)
    {
        ValidateId(receiptId);
        return LockedAsync<NativeReceiptObjectInfo?>(async () =>
        {
            var state = await LoadAsync(Name(receiptId), receiptId, token);
            if (state is null) { return null; }
            try
            {
                return new NativeReceiptObjectInfo(state.ReceiptId, state.Created, EffectiveExpiry(state), state.Size,
                Convert.ToHexString(state.Digest), state.Deleted || Expired(state));
            }
            finally { state.Clear(); }
        }, token);
    }

    public Task DeleteAsync(string receiptId, CancellationToken token = default)
    {
        ValidateId(receiptId);
        return LockedAsync(async () =>
        {
            var name = Name(receiptId);
            var state = await LoadAsync(name, receiptId, token);
            if (state is null) { throw new NativeReceiptStorageException(); }
            try
            {
                if (!state.Deleted) { await SaveAsync(name, state with { Content = Array.Empty<byte>(), Deleted = true }, replace: true, token); }
            }
            finally { state.Clear(); }
            return true;
        }, token);
    }

    public Task<byte[]> ExportEncryptedAsync(string receiptId, CancellationToken token = default)
    {
        ValidateId(receiptId);
        return LockedAsync(async () =>
        {
            var name = Name(receiptId);
            var envelope = await files.ReadAsync(name, MaximumEnvelope, token) ?? throw new NativeReceiptStorageException();
            ObjectState? state = null;
            try
            {
                state = Decode(envelope, name, receiptId);
                if (EffectiveExpiry(state) < state.Expires || !state.Deleted && Expired(state))
                {
                    CryptographicOperations.ZeroMemory(envelope);
                    var exported = state with { Expires = EffectiveExpiry(state) };
                    if (Expired(state)) { exported = exported with { Content = Array.Empty<byte>(), Deleted = true }; }
                    envelope = Encode(exported, name);
                }
                return envelope;
            }
            catch { CryptographicOperations.ZeroMemory(envelope); throw; }
            finally { state?.Clear(); }
        }, token);
    }

    public Task RestoreEncryptedAsync(string receiptId, ReadOnlyMemory<byte> envelope, CancellationToken token = default)
    {
        ValidateId(receiptId);
        if (envelope.Length is < 32 or > MaximumEnvelope) { throw new ArgumentException("Invalid encrypted receipt backup size."); }
        return LockedAsync(async () =>
        {
            var name = Name(receiptId);
            var state = Decode(envelope.ToArray(), name, receiptId);
            ObjectState? existing = null;
            try
            {
                existing = await LoadAsync(name, receiptId, token);
                if (existing is not null)
                {
                    if (existing.Created != state.Created || state.Expires > existing.Expires || existing.Size != state.Size
                        || !CryptographicOperations.FixedTimeEquals(existing.Digest, state.Digest)) { throw new NativeReceiptStorageException(); }
                    if (existing.Deleted || Expired(existing)) { return true; }
                    if (!state.Deleted && !Expired(state) && state.Expires == existing.Expires) { return true; }
                }
                else if (files.ListFiles().Count(item => item != MarkerName && item.EndsWith(".object", StringComparison.Ordinal)) >= options.MaximumObjects)
                { throw new NativeReceiptStorageException(); }
                var restored = state.Deleted || Expired(state) ? state with { Content = Array.Empty<byte>(), Deleted = true } : state;
                await SaveAsync(name, restored, replace: existing is not null, token);
                return true;
            }
            finally { existing?.Clear(); state.Clear(); }
        }, token);
    }

    public Task<int> CleanupBatchAsync(CancellationToken token = default) => LockedAsync(async () =>
    {
        var names = files.ListFiles().Where(name => name != MarkerName);
        var batch = names.Where(name => cleanupAfter is null || string.CompareOrdinal(name, cleanupAfter) > 0).Take(25).ToArray();
        var removed = 0;
        foreach (var name in batch)
        {
            token.ThrowIfCancellationRequested();
            if (name.EndsWith(".tmp", StringComparison.Ordinal)) { files.DeleteTemporary(name); removed++; continue; }
            var state = await LoadAsync(name, expectedId: null, token) ?? throw new NativeReceiptStorageException();
            try
            {
                if (!state.Deleted && Expired(state))
                { await SaveAsync(name, state with { Content = Array.Empty<byte>(), Deleted = true }, replace: true, token); removed++; }
            }
            finally { state.Clear(); }
        }
        cleanupAfter = batch.Length < 25 ? null : batch[^1];
        return removed;
    }, token);

    private async Task<ObjectState?> LoadAsync(string name, string? expectedId, CancellationToken token)
    {
        var bytes = await files.ReadAsync(name, MaximumEnvelope, token);
        if (bytes is null) { return null; }
        try { return Decode(bytes, name, expectedId); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private async Task SaveAsync(string name, ObjectState state, bool replace, CancellationToken token)
    {
        var bytes = Encode(state, name);
        try { await files.WriteAsync(name, bytes, replace, token); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private byte[] Encode(ObjectState state, string name)
    {
        using var plain = new MemoryStream();
        try
        {
            using (var writer = new BinaryWriter(plain, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write((byte)1); writer.Write(state.ReceiptId); writer.Write(state.Created.UtcTicks); writer.Write(state.Expires.UtcTicks);
                writer.Write(state.Deleted); writer.Write(state.Size); writer.Write(state.Digest); writer.Write(state.Content.Length); writer.Write(state.Content);
            }
            var keyId = Encoding.ASCII.GetBytes(options.ActiveEncryptionKeyId);
            var result = new byte[30 + keyId.Length + plain.Length];
            result[0] = 1; result[1] = (byte)keyId.Length; keyId.CopyTo(result, 2);
            var nonce = result.AsSpan(2 + keyId.Length, 12);
            RandomNumberGenerator.Fill(nonce);
            using var aes = new AesGcm(keys[options.ActiveEncryptionKeyId], 16);
            aes.Encrypt(nonce, plain.GetBuffer().AsSpan(0, checked((int)plain.Length)), result.AsSpan(30 + keyId.Length),
                result.AsSpan(14 + keyId.Length, 16), AssociatedData(result.AsSpan(0, 2 + keyId.Length), name));
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(plain.GetBuffer()); }
    }

    private ObjectState Decode(byte[] envelope, string name, string? expectedId)
    {
        if (envelope.Length is < 32 or > MaximumEnvelope || envelope[0] != 1 || envelope[1] is < 1 or > 32 || envelope.Length < 30 + envelope[1])
        { throw new NativeReceiptStorageException(); }
        var length = envelope[1];
        var keyId = Encoding.ASCII.GetString(envelope, 2, length);
        if (!keys.TryGetValue(keyId, out var key)) { throw new NativeReceiptStorageException(); }
        var plain = new byte[envelope.Length - 30 - length];
        ObjectState? state = null;
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(envelope.AsSpan(2 + length, 12), envelope.AsSpan(30 + length), envelope.AsSpan(14 + length, 16), plain,
                AssociatedData(envelope.AsSpan(0, 2 + length), name));
            using var stream = new MemoryStream(plain, writable: false);
            using var reader = new BinaryReader(stream, Encoding.UTF8);
            if (reader.ReadByte() != 1) { throw new NativeReceiptStorageException(); }
            var id = reader.ReadString(); ValidateId(id);
            var created = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
            var expires = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
            var deletedByte = reader.ReadByte();
            if (deletedByte > 1) { throw new NativeReceiptStorageException(); }
            var deleted = deletedByte == 1;
            var size = reader.ReadInt32();
            var digest = reader.ReadBytes(32);
            var contentSize = reader.ReadInt32();
            if (contentSize is < 0 or > (int)ReceiptProcessingService.MaximumReceiptSizeBytes || stream.Length - stream.Position != contentSize)
            { throw new NativeReceiptStorageException(); }
            state = new ObjectState(id, created, expires, size, digest, reader.ReadBytes(contentSize), deleted);
            var hours = (expires - created).TotalHours;
            if (Name(id) != name || expectedId is not null && expectedId != id || created == default || hours is < 1 or > 24 || hours != Math.Truncate(hours)
                || size is < 1 or > (int)ReceiptProcessingService.MaximumReceiptSizeBytes || digest.Length != 32 || deleted && contentSize != 0
                || !deleted && (contentSize != size || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(state.Content), digest)))
            { throw new NativeReceiptStorageException(); }
            return state;
        }
        catch { state?.Clear(); throw new NativeReceiptStorageException(); }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    private static byte[] AssociatedData(ReadOnlySpan<byte> header, string name)
    {
        var scope = Encoding.ASCII.GetBytes("financial-assistant|receipt-object|v1|" + name);
        var result = new byte[header.Length + scope.Length]; header.CopyTo(result); scope.CopyTo(result, header.Length); return result;
    }
    private string Name(string id) => Convert.ToHexString(HMACSHA256.HashData(indexKey, Encoding.ASCII.GetBytes("receipt-object|" + id))) + ".object";
    private DateTimeOffset EffectiveExpiry(ObjectState state) => state.Expires - state.Created > TimeSpan.FromHours(retentionHours)
        ? state.Created.AddHours(retentionHours) : state.Expires;
    private bool Expired(ObjectState state) => clock.GetUtcNow() >= EffectiveExpiry(state);
    private void ValidateKeyMarker()
    {
        var expected = new KeyMarker(1, retentionHours, Convert.ToHexString(HMACSHA256.HashData(indexKey, "receipt-index-key-v1"u8)),
            keys.ToDictionary(pair => pair.Key, pair => Convert.ToHexString(HMACSHA256.HashData(pair.Value,
                Encoding.ASCII.GetBytes("receipt-encryption-key-v1|" + pair.Key))), StringComparer.Ordinal), "");
        var previous = files.ReadAsync(MarkerName, 8192).GetAwaiter().GetResult();
        if (previous is null)
        {
            if (files.ListFiles().Any(name => name.EndsWith(".object", StringComparison.Ordinal))) { throw new NativeReceiptStorageException(); }
        }
        else
        {
            var marker = JsonSerializer.Deserialize<KeyMarker>(previous, new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow })
                ?? throw new NativeReceiptStorageException();
            if (marker.Version != 1 || marker.RetentionHours is < 1 or > 24 || marker.IndexHash != expected.IndexHash || marker.Keys is null || marker.Keys.Count is < 1 or > 8
                || marker.AuthenticationCode is null || marker.AuthenticationCode.Length != 64
                || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(marker.AuthenticationCode), MarkerMac(marker))
                || marker.Keys.Any(pair => !expected.Keys.TryGetValue(pair.Key, out var value) || value != pair.Value))
            { throw new NativeReceiptStorageException(); }
            retentionHours = Math.Min(retentionHours, marker.RetentionHours);
            expected = expected with { RetentionHours = retentionHours };
            if (marker.Keys.Count == expected.Keys.Count && marker.RetentionHours == retentionHours) { return; }
        }
        expected = expected with { AuthenticationCode = Convert.ToHexString(MarkerMac(expected)) };
        files.WriteAsync(MarkerName, JsonSerializer.SerializeToUtf8Bytes(expected), replace: previous is not null).GetAwaiter().GetResult();
    }
    private byte[] MarkerMac(KeyMarker marker)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Purpose = "receipt-key-marker-v1",
            marker.Version,
            marker.RetentionHours,
            marker.IndexHash,
            Keys = marker.Keys.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new { pair.Key, pair.Value }).ToArray()
        });
        return HMACSHA256.HashData(indexKey, bytes);
    }
    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 100 || id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-')))
        { throw new ArgumentException("A bounded opaque receipt identity is required."); }
    }
    private async Task<T> LockedAsync<T>(Func<Task<T>> operation, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try { ObjectDisposedException.ThrowIf(disposed, this); return await operation(); }
        catch (OperationCanceledException) { throw; }
        catch (ObjectDisposedException) { throw; }
        catch { throw new NativeReceiptStorageException(); }
        finally { gate.Release(); }
    }
    public void Dispose()
    {
        gate.Wait();
        try
        {
            if (disposed) { return; }
            disposed = true; files.Dispose(); CryptographicOperations.ZeroMemory(indexKey);
            foreach (var key in keys.Values) { CryptographicOperations.ZeroMemory(key); }
        }
        finally { gate.Release(); }
    }
    private sealed record ObjectState(string ReceiptId, DateTimeOffset Created, DateTimeOffset Expires, int Size, byte[] Digest, byte[] Content, bool Deleted)
    { public void Clear() { CryptographicOperations.ZeroMemory(Digest); CryptographicOperations.ZeroMemory(Content); } }
    private sealed class ZeroingStream : MemoryStream
    {
        private readonly byte[] bytes;
        public ZeroingStream(byte[] bytes) : base(bytes, writable: false) { this.bytes = bytes; }
        protected override void Dispose(bool disposing) { CryptographicOperations.ZeroMemory(bytes); base.Dispose(disposing); }
    }
    private sealed record KeyMarker([property: JsonRequired] int Version, [property: JsonRequired] int RetentionHours, [property: JsonRequired] string IndexHash,
        [property: JsonRequired] Dictionary<string, string> Keys, [property: JsonRequired] string AuthenticationCode);
}

public sealed record NativeReceiptObjectInfo(string ReceiptId, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc,
    int SizeBytes, string ContentDigest, bool DeletedOrExpired);
public sealed class NativeReceiptStorageException : IOException
{ public NativeReceiptStorageException() : base("Native receipt storage is unavailable.") { } }
