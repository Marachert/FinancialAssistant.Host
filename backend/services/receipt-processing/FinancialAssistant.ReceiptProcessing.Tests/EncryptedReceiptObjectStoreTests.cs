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
}
