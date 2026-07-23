using System.Net.Http.Headers;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Events;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Ocr;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.ReceiptProcessing.Tests;

public sealed class OcrBoundaryTests : IClassFixture<ReceiptProcessingWebApplicationFactory>
{
    private readonly IServiceProvider services;
    private readonly HttpClient client;

    public OcrBoundaryTests(ReceiptProcessingWebApplicationFactory factory)
    {
        client = factory.CreateClient();
        services = factory.Services;
    }

    [Fact]
    public void Normalizer_ConvertsInvalidProviderMetadataToSafeAmbiguityCodes()
    {
        var normalizer = new DeterministicReceiptCandidateNormalizer();
        var extraction = new OcrExtractionResult(
            "10.00 USD",
            2.5m,
            new[] { "unsafe ambiguity value" });

        var candidate = normalizer.Normalize(extraction);

        Assert.Equal(0, candidate.Confidence);
        Assert.Contains("invalid_confidence", candidate.Ambiguities);
        Assert.Contains("ocr_ambiguity_invalid", candidate.Ambiguities);
        Assert.DoesNotContain("unsafe ambiguity value", candidate.Ambiguities);
    }

    [Fact]
    public async Task ReceiptUploadedConsumer_RejectsEventWithMismatchedOwnership()
    {
        const string userId = "synthetic-event-owner";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/receipts");
        request.Headers.Add(
            ReceiptProcessingHeaders.GatewayAuthentication,
            ReceiptProcessingWebApplicationFactory.GatewaySecret);
        request.Headers.Add(ReceiptProcessingHeaders.GatewayUserId, userId);
        request.Headers.Add(ReceiptProcessingHeaders.IdempotencyKey, "receipt-event-ownership");
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(
            new byte[]
            {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x0D
            });
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        multipart.Add(file, "file", "ignored.png");
        request.Content = multipart;
        var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode);

        var published = services.GetRequiredService<InMemoryReceiptUploadedPublisher>()
            .PublishedEvents
            .Single(item => item.UserId == userId);

        var forged = published with
        {
            UserId = "synthetic-forged-owner",
            EventId = "event_synthetic_forged"
        };
        var consumer = services.GetRequiredService<IReceiptUploadedConsumer>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.ConsumeAsync(forged, CancellationToken.None));
    }
}
