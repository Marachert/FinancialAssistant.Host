using System.Reflection;
using System.Security.Cryptography;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;

namespace FinancialAssistant.ReceiptProcessing.Tests;

public sealed class EncryptedReceiptObjectStoreTests
{
    [Fact]
    public async Task Store_RoundTripsContentOnlyThroughAuthenticatedDecryption()
    {
        using var store = new EncryptedInMemoryReceiptObjectStore();
        var content = "synthetic-receipt-payload"u8.ToArray();

        await store.StoreAsync("receipt_synthetic_encrypted", content, CancellationToken.None);
        await using var stored = await store.OpenReadAsync(
            "receipt_synthetic_encrypted",
            CancellationToken.None);

        Assert.NotNull(stored);
        using var output = new MemoryStream();
        await stored.CopyToAsync(output);
        Assert.Equal(content, output.ToArray());
        Assert.Null(await store.OpenReadAsync("receipt_other", CancellationToken.None));
    }

    [Fact]
    public async Task Store_EncryptsAndRejectsTamperedCiphertext()
    {
        using var store = new EncryptedInMemoryReceiptObjectStore();
        const string receiptId = "receipt_synthetic_tamper";
        var content = "synthetic-receipt-payload"u8.ToArray();

        await store.StoreAsync(receiptId, content, CancellationToken.None);

        var objectsField = typeof(EncryptedInMemoryReceiptObjectStore).GetField(
            "objects",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(objectsField);
        var objects = Assert.IsAssignableFrom<object>(objectsField.GetValue(store));
        var tryGetValue = objects.GetType().GetMethod("TryGetValue");
        Assert.NotNull(tryGetValue);
        object?[] arguments = { receiptId, null };
        Assert.True(Assert.IsType<bool>(tryGetValue.Invoke(objects, arguments)));
        var encryptedObject = Assert.IsAssignableFrom<object>(arguments[1]);
        var ciphertextProperty = encryptedObject.GetType().GetProperty("Ciphertext");
        Assert.NotNull(ciphertextProperty);
        var ciphertext = Assert.IsType<byte[]>(ciphertextProperty.GetValue(encryptedObject));

        Assert.False(content.AsSpan().SequenceEqual(ciphertext));
        ciphertext[0] ^= 0x01;

        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(async () =>
            await store.OpenReadAsync(receiptId, CancellationToken.None));
    }
}
