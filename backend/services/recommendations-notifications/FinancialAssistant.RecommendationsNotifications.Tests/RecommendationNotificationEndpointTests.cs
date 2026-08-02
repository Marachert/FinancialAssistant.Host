using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Contracts;
using FinancialAssistant.Shared.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinancialAssistant.RecommendationsNotifications.Tests;

public sealed class RecommendationNotificationEndpointTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Endpoints_RequireTrustedGateway()
    {
        await using var factory = new RecommendationNotificationWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"{RecommendationNotificationApiRoutes.Recommendations}?currency=USD");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Endpoints_ReturnPreparedStateAndUpdateDeliveryStatus()
    {
        await using var factory = new RecommendationNotificationWebApplicationFactory();
        var ownerHash = RecommendationNotificationOwnerHasher.Hash("user-1");
        using (var scope = factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<RecommendationService>();
            await service.ProcessAnalyticsAsync(
                new IntegrationEventEnvelope<AnalyticsUpdatedV1>(
                    "analytics-api-1",
                    "analytics-api-occurrence-1",
                    AnalyticsEventTypes.AnalyticsUpdated,
                    Now,
                    "financial-assistant-analytics-service",
                    AnalyticsEventTypes.SchemaVersion,
                    "correlation-api-1",
                    "financial-api-1",
                    ownerHash,
                    new AnalyticsUpdatedV1(
                        "USD",
                        new DateOnly(2026, 8, 2),
                        1_000m,
                        900m,
                        100m,
                        20m,
                        "food",
                        Now)),
                CancellationToken.None);
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            RecommendationNotificationGatewayHeaders.Authentication,
            RecommendationNotificationWebApplicationFactory.SharedSecret);
        client.DefaultRequestHeaders.Add(
            RecommendationNotificationGatewayHeaders.UserId,
            "user-1");
        var recommendationResponse = await client.GetAsync(
            $"{RecommendationNotificationApiRoutes.Recommendations}?currency=USD");
        var notificationResponse = await client.GetAsync(
            $"{RecommendationNotificationApiRoutes.Notifications}?currency=USD");
        var notifications = await notificationResponse.Content
            .ReadFromJsonAsync<NotificationListResponse>();
        var first = notifications!.Items[0];
        var updateResponse = await client.PutAsJsonAsync(
            RecommendationNotificationApiRoutes.NotificationStatus
                .Replace("{notificationId}", first.NotificationId, StringComparison.Ordinal),
            new UpdateNotificationDeliveryStatusRequest(
                "delivered",
                Now.AddMinutes(1)));

        Assert.Equal(HttpStatusCode.OK, recommendationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, notificationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.Equal("delivered", updated!.DeliveryStatus);
    }
}
