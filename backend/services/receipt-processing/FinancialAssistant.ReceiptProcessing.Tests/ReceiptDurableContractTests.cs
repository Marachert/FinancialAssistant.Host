using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Domain;
using FinancialAssistant.ReceiptProcessing.Infrastructure;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Ocr;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.ReceiptProcessing.Tests;

public sealed class ReceiptDurableContractTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReceiptAndOcrContextsCannotUseAnotherServicesRole()
    {
        using var keys = new RecordProtection(new byte[32], "v1", new Dictionary<string, byte[]> { ["v1"] = Enumerable.Repeat((byte)2, 32).ToArray() });
        await using var database = new OwnedPostgresStore(new OwnedPostgresOptions(new StorageIdentity("ai-orchestration"), "synthetic", 65432, new string('x', 32)), keys);
        Assert.Throws<ArgumentException>(() => new ReceiptPersistenceContext(database));
    }

    [Theory]
    [InlineData("../receipt")]
    [InlineData("receipt\nforged")]
    [InlineData("")]
    public void InvalidIdentityFailsBeforeDatabaseAccess(string id) => Assert.Throws<ArgumentException>(() => ReceiptRecordCodec.Identifier(id));

    [Fact]
    public void CandidateDecimalAndAuthorityStayDeterministicAndRawInputIsAbsent()
    {
        var value = Result();
        var bytes = OwnedRecordCodec.Encode(1, value);
        var restored = OwnedRecordCodec.Decode<StoredOcrProcessing>(1, new DurableRecord(0, bytes));
        ReceiptRecordCodec.Processing(restored);
        Assert.Equal(1234567890.123456789m, restored.IntegrationEvent!.Amount);
        Assert.Equal("suggestion", ReceiptCandidateAuthority.Suggestion);
        Assert.DoesNotContain("extractedText", System.Text.Encoding.UTF8.GetString(bytes));
        Assert.False(restored.Metadata.OcrCompletedPublished);
    }

    [Fact]
    public void MismatchedCandidateOwnerAndUnsafeAuditAreRejected()
    {
        var value = Result();
        Assert.Throws<ArgumentException>(() => ReceiptRecordCodec.Processing(value with { IntegrationEvent = value.IntegrationEvent! with { UserId = "other" } }));
        Assert.Throws<ArgumentException>(() => ReceiptRecordCodec.Processing(value with
        {
            Metadata = value.Metadata with
            { Audit = value.Metadata.Audit with { FailureCategory = "raw provider content" } }
        }));
        Assert.Throws<ArgumentException>(() => ReceiptRecordCodec.Processing(value with { Metadata = value.Metadata with { Status = "confirmed" } }));
    }

    [Fact]
    public void NormalizedZeroTotalPersistsAsSuggestionWhileNegativeAmountsRemainInvalid()
    {
        var candidate = new DeterministicReceiptCandidateNormalizer().Normalize(
            new OcrExtractionResult("merchant: Synthetic Store\ntotal: 0.00 USD\n2026-10-19", 0.75m, Array.Empty<string>()));
        Assert.Equal(0m, candidate.Amount);
        Assert.True(candidate.RequiresReview);
        var value = Result();
        var zero = value with { IntegrationEvent = value.IntegrationEvent! with { Amount = candidate.Amount } };
        ReceiptRecordCodec.Processing(zero);
        var restored = OwnedRecordCodec.Decode<StoredOcrProcessing>(1,
            new DurableRecord(0, OwnedRecordCodec.Encode(1, zero)));
        ReceiptRecordCodec.Processing(restored);
        Assert.Equal(0m, restored.IntegrationEvent!.Amount);
        Assert.Throws<ArgumentException>(() => ReceiptRecordCodec.Processing(zero with
        { IntegrationEvent = zero.IntegrationEvent! with { Amount = -0.01m } }));
    }

    [Fact]
    public void NativePocCannotEnableLiveProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ReceiptProcessing:Ocr:Enabled"] = "true" }).Build();
        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddReceiptProcessingInfrastructure(configuration, true));
        Assert.Contains("Live OCR", error.Message);
    }

    [Fact]
    public void UploadOutboxIdentityIsStableAndNeverAcknowlegesStaging()
    {
        var file = new ReceiptFileMetadata("receipt_synthetic", "synthetic_owner", "image/png", 8, new string('A', 64), Now, "event_upload", false);
        var value = ReceiptOutboxMessage.For(file);
        Assert.Equal("uploaded:event_upload", value.Key);
        Assert.Equal(file.UploadedAtUtc, value.OccurredAtUtc);
        Assert.Null(value.PublishedAtUtc);
        Assert.False(new PostgresReceiptOutbox(null!).ConfirmsDelivery);
    }

    private static StoredOcrProcessing Result()
    {
        var metadata = new ReceiptOcrMetadata("receipt_synthetic", "synthetic_owner", ReceiptProcessingStatuses.OcrCompleted,
            0.75m, new[] { "low_confidence" }, new OcrProcessingAuditMetadata("event_upload", "synthetic", "synthetic-model", 1, 0.75m,
                null, "event_upload", new OcrProviderUsageMetadata(8, 0, "2026-10")), Now, false);
        var value = new OcrCompletedIntegrationEvent(OcrCompletedIntegrationEvent.Name, "event_completed", metadata.ReceiptId, metadata.UserId,
            "expense", 1234567890.123456789m, "USD", null, "Synthetic Store", new DateOnly(2026, 10, 19), 0.75m, metadata.Ambiguities, Now);
        return new StoredOcrProcessing(metadata, value);
    }
}
