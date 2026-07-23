using System.Net;
using System.Text.Json;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.ReceiptProcessing.Infrastructure.Events;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.ReceiptProcessing.Tests;

public sealed class HttpOcrCompletedPublisherTests
{
    [Fact]
    public async Task Publish_SendsAuthenticatedEventToTransactionIntake()
    {
        var handler = new RecordingHandler(HttpStatusCode.NoContent);
        var publisher = CreatePublisher(handler);
        var integrationEvent = CreateEvent();

        await publisher.PublishAsync(integrationEvent, CancellationToken.None);

        Assert.Equal(
            new Uri("https://transaction-intake.internal/internal/events/ocr-completed"),
            handler.RequestUri);
        Assert.Equal(
            "synthetic-interservice-secret-value",
            handler.EventAuthentication);
        Assert.NotNull(handler.Body);
        var delivered = JsonSerializer.Deserialize<OcrCompletedIntegrationEvent>(
            handler.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(delivered);
        Assert.Equal(integrationEvent.EventType, delivered.EventType);
        Assert.Equal(integrationEvent.EventId, delivered.EventId);
        Assert.Equal(integrationEvent.ReceiptId, delivered.ReceiptId);
        Assert.Equal(integrationEvent.UserId, delivered.UserId);
        Assert.Equal(integrationEvent.Amount, delivered.Amount);
        Assert.Equal(
            integrationEvent.Ambiguities.ToArray(),
            delivered.Ambiguities.ToArray());
    }

    [Fact]
    public async Task Publish_WhenTransactionIntakeRejectsEvent_ThrowsForRetry()
    {
        var publisher = CreatePublisher(
            new RecordingHandler(HttpStatusCode.ServiceUnavailable));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            publisher.PublishAsync(CreateEvent(), CancellationToken.None));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
    }

    private static HttpOcrCompletedPublisher CreatePublisher(
        HttpMessageHandler handler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [HttpOcrCompletedPublisher.BaseAddressConfigurationKey] =
                        "https://transaction-intake.internal",
                    [HttpOcrCompletedPublisher.SharedSecretConfigurationKey] =
                        "synthetic-interservice-secret-value"
                })
            .Build();
        return new HttpOcrCompletedPublisher(
            new StubHttpClientFactory(new HttpClient(handler)),
            configuration);
    }

    private static OcrCompletedIntegrationEvent CreateEvent() =>
        new(
            OcrCompletedIntegrationEvent.Name,
            "event_http_publisher",
            "receipt_http_publisher",
            "user_http_publisher",
            "expense",
            12.34m,
            "USD",
            "expense.other",
            "Synthetic Store",
            new DateOnly(2026, 7, 24),
            0.91m,
            new[] { "merchant_uncertain" },
            new DateTimeOffset(2026, 7, 24, 0, 0, 0, TimeSpan.Zero));

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient client;

        public StubHttpClientFactory(HttpClient client)
        {
            this.client = client;
        }

        public HttpClient CreateClient(string name)
        {
            Assert.Equal(HttpOcrCompletedPublisher.ClientName, name);
            return client;
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode responseStatus;

        public RecordingHandler(HttpStatusCode responseStatus)
        {
            this.responseStatus = responseStatus;
        }

        public Uri? RequestUri { get; private set; }

        public string? EventAuthentication { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            EventAuthentication = request.Headers
                .GetValues(ReceiptProcessingHeaders.EventAuthentication)
                .Single();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(responseStatus);
        }
    }
}
