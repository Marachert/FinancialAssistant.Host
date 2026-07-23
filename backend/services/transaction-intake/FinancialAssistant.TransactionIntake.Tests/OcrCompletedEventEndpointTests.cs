using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.TransactionIntake.Tests;

public sealed class OcrCompletedEventEndpointTests :
    IClassFixture<TransactionIntakeWebApplicationFactory>
{
    private readonly HttpClient client;
    private readonly IServiceProvider services;

    public OcrCompletedEventEndpointTests(
        TransactionIntakeWebApplicationFactory factory)
    {
        client = factory.CreateClient();
        services = factory.Services;
    }

    [Fact]
    public async Task EventEndpoint_AuthenticatesCreatesDraftAndIsIdempotent()
    {
        var integrationEvent = CreateEvent("receipt_http_delivery");

        using var unauthorized = await client.PostAsJsonAsync(
            ReceiptProcessingApiRoutes.OcrCompletedEvent,
            integrationEvent);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        using var firstRequest = CreateRequest(integrationEvent);
        using var first = await client.SendAsync(firstRequest);
        using var replayRequest = CreateRequest(integrationEvent);
        using var replay = await client.SendAsync(replayRequest);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
        var stored = await services
            .GetRequiredService<ITransactionDraftStore>()
            .GetAsync(
                integrationEvent.UserId,
                $"ocr-{integrationEvent.ReceiptId}",
                CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(42.10m, stored.Draft.Amount);
        Assert.True(stored.Draft.RequiresReview);
        Assert.Contains("merchant_uncertain", stored.Draft.Ambiguities);
    }

    [Fact]
    public async Task EventEndpoint_RejectsConflictingRedelivery()
    {
        var integrationEvent = CreateEvent("receipt_http_conflict");
        using var firstRequest = CreateRequest(integrationEvent);
        using var first = await client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        using var conflictRequest = CreateRequest(
            integrationEvent with
            {
                EventId = "event_http_conflict_changed",
                Amount = 99.99m
            });
        using var conflict = await client.SendAsync(conflictRequest);

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    private static HttpRequestMessage CreateRequest(
        OcrCompletedIntegrationEvent integrationEvent)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            ReceiptProcessingApiRoutes.OcrCompletedEvent)
        {
            Content = JsonContent.Create(integrationEvent)
        };
        request.Headers.Add(
            ReceiptProcessingHeaders.EventAuthentication,
            TransactionIntakeWebApplicationFactory.ReceiptEventSecret);
        return request;
    }

    private static OcrCompletedIntegrationEvent CreateEvent(string receiptId) =>
        new(
            OcrCompletedIntegrationEvent.Name,
            $"event_{receiptId}",
            receiptId,
            $"user_{receiptId}",
            "expense",
            42.10m,
            "USD",
            "expense.other",
            "Synthetic Store",
            new DateOnly(2026, 7, 24),
            0.88m,
            new[] { "merchant_uncertain" },
            new DateTimeOffset(2026, 7, 24, 0, 0, 0, TimeSpan.Zero));
}
