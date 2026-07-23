using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

public sealed class EncryptedInMemoryReceiptObjectStore : IReceiptObjectStore, IDisposable
{
    private const int TagSizeBytes = 16;
    private readonly byte[] encryptionKey = RandomNumberGenerator.GetBytes(32);
    private readonly ConcurrentDictionary<string, EncryptedReceiptObject> objects =
        new(StringComparer.Ordinal);

    public Task StoreAsync(
        string receiptId,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var ciphertext = new byte[content.Length];
        var tag = new byte[TagSizeBytes];
        using var aes = new AesGcm(encryptionKey, TagSizeBytes);
        aes.Encrypt(
            nonce,
            content.Span,
            ciphertext,
            tag,
            Encoding.UTF8.GetBytes(receiptId));
        if (!objects.TryAdd(receiptId, new EncryptedReceiptObject(nonce, ciphertext, tag)))
        {
            throw new InvalidOperationException("Receipt object already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<Stream?> OpenReadAsync(
        string receiptId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!objects.TryGetValue(receiptId, out var stored))
        {
            return Task.FromResult<Stream?>(null);
        }

        var plaintext = new byte[stored.Ciphertext.Length];
        try
        {
            using var aes = new AesGcm(encryptionKey, TagSizeBytes);
            aes.Decrypt(
                stored.Nonce,
                stored.Ciphertext,
                stored.Tag,
                plaintext,
                Encoding.UTF8.GetBytes(receiptId));
            return Task.FromResult<Stream?>(new ZeroingReadOnlyMemoryStream(plaintext));
        }
        catch
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw;
        }
    }

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(encryptionKey);
        foreach (var stored in objects.Values)
        {
            CryptographicOperations.ZeroMemory(stored.Ciphertext);
        }
    }

    private sealed record EncryptedReceiptObject(
        byte[] Nonce,
        byte[] Ciphertext,
        byte[] Tag);

    private sealed class ZeroingReadOnlyMemoryStream : MemoryStream
    {
        private readonly byte[] buffer;
        private bool disposed;

        public ZeroingReadOnlyMemoryStream(byte[] buffer)
            : base(buffer, writable: false)
        {
            this.buffer = buffer;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                CryptographicOperations.ZeroMemory(buffer);
                disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}
