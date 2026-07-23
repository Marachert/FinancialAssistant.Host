using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Events;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.ReceiptProcessing.Tests;

public sealed class ReceiptEndpointTests : IClassFixture<ReceiptProcessingWebApplicationFactory>
{
    private static readonly byte[] SyntheticPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D
    };

    private readonly HttpClient client;
    private readonly IServiceProvider services;

    public ReceiptEndpointTests(ReceiptProcessingWebApplicationFactory factory)
    {
        client = factory.CreateClient();
        services = factory.Services;
    }

    [Fact]
    public async Task Upload_StoresSafeMetadataRunsOcrAndCreatesReviewableDraft()
    {
        const string userId = "synthetic-receipt-owner";
        using var request = CreateUploadRequest(
            userId,
            "receipt-upload-001",
            SyntheticPng,
            "image/png");

        var response = await client.SendAsync(request);
        var receipt = await response.Content.ReadFromJsonAsync<ReceiptResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(receipt);
        Assert.Equal("ocr_completed", receipt.Status);
        Assert.Equal("image/png", receipt.ContentType);
        Assert.Equal(SyntheticPng.Length, receipt.SizeBytes);
        Assert.Equal(0.91m, receipt.OcrConfidence);
        Assert.Contains("merchant_uncertain", receipt.OcrAmbiguities);

        var receiptMetadata = services.GetRequiredService<InMemoryReceiptMetadataStore>();
        var storedMetadata = Assert.Single(
            receiptMetadata.Records,
            item => item.ReceiptId == receipt.ReceiptId);
        Assert.True(storedMetadata.ReceiptUploadedPublished);

        var ocrMetadata = services.GetRequiredService<InMemoryOcrProcessingStore>();
        var storedOcr = Assert.Single(
            ocrMetadata.Records,
            item => item.ReceiptId == receipt.ReceiptId);
        Assert.True(storedOcr.OcrCompletedPublished);
        Assert.Equal(0.91m, storedOcr.Confidence);

        var receiptEvents = services.GetRequiredService<InMemoryReceiptUploadedPublisher>();
        Assert.Single(
            receiptEvents.PublishedEvents,
            item => item.ReceiptId == receipt.ReceiptId);
        var ocrEvents = services.GetRequiredService<InMemoryOcrCompletedPublisher>();
        Assert.Single(
            ocrEvents.PublishedEvents,
            item => item.ReceiptId == receipt.ReceiptId);

        var draftStore = services.GetRequiredService<ITransactionDraftStore>();
        var storedDraft = await draftStore.GetAsync(
            userId,
            $"ocr-{receipt.ReceiptId}",
            CancellationToken.None);
        Assert.NotNull(storedDraft);
        Assert.Equal("expense", storedDraft.Draft.Type);
        Assert.Equal(123.45m, storedDraft.Draft.Amount);
        Assert.Equal("USD", storedDraft.Draft.Currency);
        Assert.Equal("expense.other", storedDraft.Draft.CategoryId);
        Assert.Equal("Synthetic Market", storedDraft.Draft.Merchant);
        Assert.Equal(0.91m, storedDraft.Draft.Confidence);
        Assert.True(storedDraft.Draft.RequiresReview);
        Assert.Contains("merchant_uncertain", storedDraft.Draft.Ambiguities);
    }

    [Fact]
    public async Task Upload_WithSameKeyAndContent_ReplaysWithoutDuplicateEvents()
    {
        const string userId = "synthetic-receipt-replay";
        using var firstRequest = CreateUploadRequest(
            userId,
            "receipt-upload-002",
            SyntheticPng,
            "image/png");
        var firstResponse = await client.SendAsync(firstRequest);
        var first = await firstResponse.Content.ReadFromJsonAsync<ReceiptResponse>();
        Assert.NotNull(first);

        using var replayRequest = CreateUploadRequest(
            userId,
            "receipt-upload-002",
            SyntheticPng,
            "image/png");
        var replayResponse = await client.SendAsync(replayRequest);
        var replay = await replayResponse.Content.ReadFromJsonAsync<ReceiptResponse>();

        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.NotNull(replay);
        Assert.Equal(first.ReceiptId, replay.ReceiptId);
        var receiptEvents = services.GetRequiredService<InMemoryReceiptUploadedPublisher>();
        Assert.Single(
            receiptEvents.PublishedEvents,
            item => item.ReceiptId == first.ReceiptId);
        var ocrEvents = services.GetRequiredService<InMemoryOcrCompletedPublisher>();
        Assert.Single(
            ocrEvents.PublishedEvents,
            item => item.ReceiptId == first.ReceiptId);
    }

    [Fact]
    public async Task Upload_WithSpoofedImageSignature_ReturnsUnsupportedMediaType()
    {
        using var request = CreateUploadRequest(
            "synthetic-receipt-spoof",
            "receipt-upload-003",
            "not-an-image"u8.ToArray(),
            "image/png");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.DoesNotContain(
            services.GetRequiredService<InMemoryReceiptMetadataStore>().Records,
            item => item.UserId == "synthetic-receipt-spoof");
    }

    [Fact]
    public async Task Upload_WithoutTrustedGatewayAuthentication_ReturnsUnauthorized()
    {
        using var request = CreateUploadRequest(
            "synthetic-receipt-no-auth",
            "receipt-upload-004",
            SyntheticPng,
            "image/png");
        request.Headers.Remove(ReceiptProcessingHeaders.GatewayAuthentication);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_IsScopedToAuthenticatedUser()
    {
        using var upload = CreateUploadRequest(
            "synthetic-receipt-private-owner",
            "receipt-upload-005",
            SyntheticPng,
            "image/png");
        var uploadResponse = await client.SendAsync(upload);
        var receipt = await uploadResponse.Content.ReadFromJsonAsync<ReceiptResponse>();
        Assert.NotNull(receipt);

        using var otherUserRequest = CreateGatewayRequest(
            HttpMethod.Get,
            $"/receipts/{receipt.ReceiptId}",
            "synthetic-receipt-private-other");
        var otherUserResponse = await client.SendAsync(otherUserRequest);

        Assert.Equal(HttpStatusCode.NotFound, otherUserResponse.StatusCode);
    }

    [Fact]
    public async Task OcrCompletedDelivery_IsIdempotentAndRejectsConflictingCandidate()
    {
        const string userId = "synthetic-ocr-delivery";
        using var upload = CreateUploadRequest(
            userId,
            "receipt-upload-006",
            SyntheticPng,
            "image/png");
        var uploadResponse = await client.SendAsync(upload);
        var receipt = await uploadResponse.Content.ReadFromJsonAsync<ReceiptResponse>();
        Assert.NotNull(receipt);

        var publisher = services.GetRequiredService<InMemoryOcrCompletedPublisher>();
        var integrationEvent = Assert.Single(
            publisher.PublishedEvents,
            item => item.ReceiptId == receipt.ReceiptId);
        var consumer = services.GetRequiredService<IOcrCompletedConsumer>();
        var draftStore = services.GetRequiredService<ITransactionDraftStore>();
        var before = await draftStore.GetAsync(
            userId,
            $"ocr-{receipt.ReceiptId}",
            CancellationToken.None);
        Assert.NotNull(before);

        await consumer.ConsumeAsync(integrationEvent, CancellationToken.None);
        var replayed = await draftStore.GetAsync(
            userId,
            $"ocr-{receipt.ReceiptId}",
            CancellationToken.None);
        Assert.NotNull(replayed);
        Assert.Equal(before.Draft.Id, replayed.Draft.Id);

        var conflicting = integrationEvent with
        {
            EventId = "event_synthetic_ocr_conflict",
            Ambiguities = new[] { "different_candidate" }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.ConsumeAsync(conflicting, CancellationToken.None));
    }

    private static HttpRequestMessage CreateUploadRequest(
        string userId,
        string idempotencyKey,
        byte[] bytes,
        string contentType)
    {
        var request = CreateGatewayRequest(HttpMethod.Post, "/receipts", userId);
        request.Headers.Add(ReceiptProcessingHeaders.IdempotencyKey, idempotencyKey);
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        multipart.Add(file, "file", "ignored-client-filename.png");
        request.Content = multipart;
        return request;
    }

    private static HttpRequestMessage CreateGatewayRequest(
        HttpMethod method,
        string path,
        string userId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(
            ReceiptProcessingHeaders.GatewayAuthentication,
            ReceiptProcessingWebApplicationFactory.GatewaySecret);
        request.Headers.Add(ReceiptProcessingHeaders.GatewayUserId, userId);
        return request;
    }
}
