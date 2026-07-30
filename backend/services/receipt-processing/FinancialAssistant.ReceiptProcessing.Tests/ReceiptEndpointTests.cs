using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinancialAssistant.ReceiptProcessing.Application;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Events;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Storage;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Application.Drafts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        Assert.Equal(storedMetadata.ReceiptUploadedEventId, storedOcr.Audit.RequestId);
        Assert.Equal("synthetic-ocr", storedOcr.Audit.ProviderName);
        Assert.Equal("synthetic-v1", storedOcr.Audit.ModelKey);
        Assert.Equal(0.91m, storedOcr.Audit.Confidence);
        Assert.Null(storedOcr.Audit.FailureCategory);
        Assert.Equal(storedOcr.Audit.RequestId, storedOcr.Audit.TraceId);
        Assert.True(storedOcr.Audit.DurationMilliseconds >= 0);
        Assert.Equal(SyntheticPng.Length, storedOcr.Audit.ProviderUsage.RequestBytes);
        Assert.Equal(1, storedOcr.Audit.ProviderUsage.ProviderRequestUnits);
        Assert.Equal(
            storedOcr.CompletedAtUtc.ToString(
                "yyyy-MM",
                System.Globalization.CultureInfo.InvariantCulture),
            storedOcr.Audit.ProviderUsage.BillingMonth);

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
        Assert.Equal(
            storedOcr.ReceiptId,
            storedDraft.Draft.Suggestion.SourceReferenceId);
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
    public async Task Upload_WhenOcrProviderFails_StoresSafeObservableAuditMetadata()
    {
        using var factory = new ReceiptProcessingWebApplicationFactory()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(serviceCollection =>
                {
                    serviceCollection.RemoveAll<IOcrProviderClient>();
                    serviceCollection.AddSingleton<IOcrProviderClient, FailingOcrProviderClient>();
                }));
        using var failureClient = factory.CreateClient();
        using var request = CreateUploadRequest(
            "synthetic-receipt-failure",
            "receipt-upload-failure-001",
            SyntheticPng,
            "image/png");

        var response = await failureClient.SendAsync(request);
        var receipt = await response.Content.ReadFromJsonAsync<ReceiptResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(receipt);
        Assert.Equal("ocr_failed", receipt.Status);
        var stored = Assert.Single(
            factory.Services.GetRequiredService<InMemoryOcrProcessingStore>().Records,
            item => item.ReceiptId == receipt.ReceiptId);
        Assert.Equal("provider_unavailable", stored.Audit.FailureCategory);
        Assert.Null(stored.Audit.Confidence);
        Assert.Equal("synthetic-ocr", stored.Audit.ProviderName);
        Assert.Equal("synthetic-v1", stored.Audit.ModelKey);
        Assert.Equal(stored.Audit.RequestId, stored.Audit.TraceId);
        Assert.True(stored.Audit.DurationMilliseconds >= 0);
        Assert.DoesNotContain(
            stored.GetType().GetProperties(),
            property =>
                property.Name.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("StackTrace", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("ProviderResponse", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Upload_WhenDailyOcrLimitIsReached_DoesNotCallProviderAgain()
    {
        var provider = new CountingOcrProviderClient();
        using var factory = new ReceiptProcessingWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ReceiptProcessing:Ocr:UsageCostControls:PerUserDailyRequestLimit"] =
                                "1"
                        }));
                builder.ConfigureServices(serviceCollection =>
                {
                    serviceCollection.RemoveAll<IOcrProviderClient>();
                    serviceCollection.AddSingleton<IOcrProviderClient>(provider);
                    serviceCollection.RemoveAll<IReceiptObjectStore>();
                    serviceCollection.AddSingleton<CountingReceiptObjectStore>();
                    serviceCollection.AddSingleton<IReceiptObjectStore>(services =>
                        services.GetRequiredService<CountingReceiptObjectStore>());
                });
            });
        using var limitedClient = factory.CreateClient();
        const string userId = "synthetic-daily-limit-user";
        using var firstRequest = CreateUploadRequest(
            userId,
            "daily-limit-upload-001",
            SyntheticPng,
            "image/png");
        using var secondRequest = CreateUploadRequest(
            userId,
            "daily-limit-upload-002",
            SyntheticPng,
            "image/png");

        using var firstResponse = await limitedClient.SendAsync(firstRequest);
        using var secondResponse = await limitedClient.SendAsync(secondRequest);
        var secondReceipt = await secondResponse.Content.ReadFromJsonAsync<ReceiptResponse>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.NotNull(secondReceipt);
        Assert.Equal("ocr_failed", secondReceipt.Status);
        Assert.Equal(1, provider.Attempts);
        var objectStore = factory.Services
            .GetRequiredService<CountingReceiptObjectStore>();
        Assert.Equal(1, objectStore.OpenReadAttempts);
        var stored = factory.Services.GetRequiredService<InMemoryOcrProcessingStore>();
        var rejected = Assert.Single(
            stored.Records,
            item => item.ReceiptId == secondReceipt.ReceiptId);
        Assert.Equal("daily_usage_limit_exceeded", rejected.Audit.FailureCategory);
        Assert.Equal(0, rejected.Audit.ProviderUsage.ProviderRequestUnits);
    }

    [Fact]
    public async Task Upload_WhenReceiptExceedsOcrProviderSizeLimit_DoesNotCallProvider()
    {
        var provider = new CountingOcrProviderClient();
        using var factory = new ReceiptProcessingWebApplicationFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ReceiptProcessing:Ocr:UsageCostControls:MaximumProviderRequestBytes"] =
                                "8"
                        }));
                builder.ConfigureServices(serviceCollection =>
                {
                    serviceCollection.RemoveAll<IOcrProviderClient>();
                    serviceCollection.AddSingleton<IOcrProviderClient>(provider);
                });
            });
        using var limitedClient = factory.CreateClient();
        using var request = CreateUploadRequest(
            "synthetic-size-limit-user",
            "size-limit-upload-001",
            SyntheticPng,
            "image/png");

        using var response = await limitedClient.SendAsync(request);
        var receipt = await response.Content.ReadFromJsonAsync<ReceiptResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(receipt);
        Assert.Equal("ocr_failed", receipt.Status);
        Assert.Equal(0, provider.Attempts);
        var stored = factory.Services.GetRequiredService<InMemoryOcrProcessingStore>();
        var rejected = Assert.Single(
            stored.Records,
            item => item.ReceiptId == receipt.ReceiptId);
        Assert.Equal("provider_request_too_large", rejected.Audit.FailureCategory);
        Assert.Equal(SyntheticPng.Length, rejected.Audit.ProviderUsage.RequestBytes);
        Assert.Equal(0, rejected.Audit.ProviderUsage.ProviderRequestUnits);
    }

    [Fact]
    public async Task Upload_WhenOcrProviderIsDisabled_RecordsZeroExternalUsage()
    {
        using var factory = new ReceiptProcessingWebApplicationFactory()
            .WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, configuration) =>
                    configuration.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["ReceiptProcessing:Ocr:Enabled"] = "false",
                            ["ReceiptProcessing:Ocr:Mode"] = "disabled",
                            ["ReceiptProcessing:Ocr:ProviderName"] = "unconfigured",
                            ["ReceiptProcessing:Ocr:ModelKey"] = "unconfigured",
                            ["ReceiptProcessing:Ocr:Endpoint"] = "",
                            ["ReceiptProcessing:Ocr:CredentialEnvironmentVariable"] = ""
                        })));
        using var disabledClient = factory.CreateClient();
        using var request = CreateUploadRequest(
            "synthetic-disabled-provider-user",
            "disabled-provider-upload-001",
            SyntheticPng,
            "image/png");

        using var response = await disabledClient.SendAsync(request);
        var receipt = await response.Content.ReadFromJsonAsync<ReceiptResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(receipt);
        Assert.Equal("ocr_failed", receipt.Status);
        var stored = factory.Services.GetRequiredService<InMemoryOcrProcessingStore>();
        var disabled = Assert.Single(
            stored.Records,
            item => item.ReceiptId == receipt.ReceiptId);
        Assert.Equal(OcrProviderErrorCodes.ProviderDisabled, disabled.Audit.FailureCategory);
        Assert.Equal(0, disabled.Audit.ProviderUsage.ProviderRequestUnits);
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
        await Assert.ThrowsAsync<OcrCompletedDraftConflictException>(() =>
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

    private sealed class FailingOcrProviderClient : IOcrProviderClient
    {
        public string Name => "synthetic-ocr";

        public Task<OcrExtractionResult> ExtractAsync(
            ReadOnlyMemory<byte> receiptImage,
            string contentType,
            CancellationToken cancellationToken) =>
            Task.FromException<OcrExtractionResult>(
                new OcrProviderException(
                    OcrProviderErrorCodes.ProviderUnavailable,
                    isTransient: false));
    }

    private sealed class CountingOcrProviderClient : IOcrProviderClient
    {
        public string Name => "synthetic-ocr";

        public int Attempts { get; private set; }

        public Task<OcrExtractionResult> ExtractAsync(
            ReadOnlyMemory<byte> receiptImage,
            string contentType,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Attempts++;
            return Task.FromResult(
                new OcrExtractionResult(
                    "10.00 USD 2026-07-30 merchant: Synthetic Market",
                    0.9m,
                    Array.Empty<string>()));
        }
    }

    private sealed class CountingReceiptObjectStore(
        EncryptedInMemoryReceiptObjectStore inner) : IReceiptObjectStore
    {
        public int OpenReadAttempts { get; private set; }

        public Task StoreAsync(
            string receiptId,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken) =>
            inner.StoreAsync(receiptId, content, cancellationToken);

        public Task<Stream?> OpenReadAsync(
            string receiptId,
            CancellationToken cancellationToken)
        {
            OpenReadAttempts++;
            return inner.OpenReadAsync(receiptId, cancellationToken);
        }
    }
}
