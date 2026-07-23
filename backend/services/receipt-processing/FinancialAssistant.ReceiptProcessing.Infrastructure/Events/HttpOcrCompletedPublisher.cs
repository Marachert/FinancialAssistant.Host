using System.Net.Http.Json;
using FinancialAssistant.ReceiptProcessing.Application.Abstractions;
using FinancialAssistant.ReceiptProcessing.Contracts;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.ReceiptProcessing.Infrastructure.Events;

public sealed class HttpOcrCompletedPublisher : IOcrCompletedPublisher
{
    public const string ClientName = "receipt-processing-ocr-completed";
    public const string BaseAddressConfigurationKey =
        "ReceiptProcessing:TransactionIntake:BaseAddress";
    public const string SharedSecretConfigurationKey =
        "ReceiptProcessing:Events:SharedSecret";

    private readonly IHttpClientFactory httpClientFactory;
    private readonly Uri endpoint;
    private readonly string sharedSecret;

    public HttpOcrCompletedPublisher(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        this.httpClientFactory = httpClientFactory;
        var baseAddress = configuration[BaseAddressConfigurationKey]?.TrimEnd('/');
        if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var parsedBaseAddress))
        {
            throw new InvalidOperationException(
                $"{BaseAddressConfigurationKey} must contain an absolute service URL.");
        }

        var configuredSecret = configuration[SharedSecretConfigurationKey];
        if (string.IsNullOrWhiteSpace(configuredSecret) ||
            configuredSecret.Length is < 32 or > 256)
        {
            throw new InvalidOperationException(
                $"{SharedSecretConfigurationKey} must contain 32 to 256 characters.");
        }

        endpoint = new Uri(
            $"{parsedBaseAddress.AbsoluteUri.TrimEnd('/')}{ReceiptProcessingApiRoutes.OcrCompletedEvent}",
            UriKind.Absolute);
        sharedSecret = configuredSecret;
    }

    public async Task PublishAsync(
        OcrCompletedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(integrationEvent)
        };
        request.Headers.Add(
            ReceiptProcessingHeaders.EventAuthentication,
            sharedSecret);

        var client = httpClientFactory.CreateClient(ClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Transaction Intake rejected OCR completion delivery with status {(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);
        }
    }
}
