using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Contracts;
using FinancialAssistant.RecommendationsNotifications.Domain;
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
        var recommendations = await recommendationResponse.Content
            .ReadFromJsonAsync<RecommendationListResponse>();
        var recommendationId = recommendations!.Items[0].RecommendationId;
        var readResponse = await client.PutAsJsonAsync(
            RecommendationNotificationApiRoutes.RecommendationRead
                .Replace(
                    "{recommendationId}",
                    recommendationId,
                    StringComparison.Ordinal),
            new MarkRecommendationReadRequest(Now.AddMinutes(1)));
        var dismissalResponse = await client.PutAsJsonAsync(
            RecommendationNotificationApiRoutes.RecommendationDismissal
                .Replace(
                    "{recommendationId}",
                    recommendationId,
                    StringComparison.Ordinal),
            new DismissRecommendationRequest(Now.AddMinutes(2)));
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
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        var read = await readResponse.Content
            .ReadFromJsonAsync<RecommendationResponse>();
        Assert.Equal("read", read!.Status);
        Assert.Equal(Now.AddMinutes(1), read.StatusChangedAtUtc);
        Assert.Equal(HttpStatusCode.OK, dismissalResponse.StatusCode);
        var dismissed = await dismissalResponse.Content
            .ReadFromJsonAsync<RecommendationResponse>();
        Assert.Equal("dismissed", dismissed!.Status);
        Assert.Equal(Now.AddMinutes(2), dismissed.StatusChangedAtUtc);
        Assert.Equal(HttpStatusCode.OK, notificationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<NotificationResponse>();
        Assert.Equal("delivered", updated!.DeliveryStatus);
    }

    [Fact]
    public async Task NotificationPreferences_DefaultUpdateAndOwnerScope_AreStable()
    {
        await using var factory = new RecommendationNotificationWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(
            RecommendationNotificationGatewayHeaders.Authentication,
            RecommendationNotificationWebApplicationFactory.SharedSecret);
        client.DefaultRequestHeaders.Add(
            RecommendationNotificationGatewayHeaders.UserId,
            "preference-owner-a");

        var defaultResponse = await client.GetAsync(
            RecommendationNotificationApiRoutes.NotificationPreferences);
        var defaults = await defaultResponse.Content
            .ReadFromJsonAsync<NotificationPreferencesResponse>();
        var quietHours = new NotificationQuietHoursContract(
            new TimeOnly(22, 0),
            new TimeOnly(7, 0),
            "Etc/UTC");
        var updateResponse = await client.PutAsJsonAsync(
            RecommendationNotificationApiRoutes.NotificationPreferences,
            new UpdateNotificationPreferencesRequest(
                false,
                true,
                [
                    NotificationTriggerCodes.BudgetExceeded,
                    NotificationTriggerCodes.ScoreImproved
                ],
                quietHours));
        var updated = await updateResponse.Content
            .ReadFromJsonAsync<NotificationPreferencesResponse>();
        var rereadResponse = await client.GetAsync(
            RecommendationNotificationApiRoutes.NotificationPreferences);
        var reread = await rereadResponse.Content
            .ReadFromJsonAsync<NotificationPreferencesResponse>();

        using var otherClient = factory.CreateClient();
        otherClient.DefaultRequestHeaders.Add(
            RecommendationNotificationGatewayHeaders.Authentication,
            RecommendationNotificationWebApplicationFactory.SharedSecret);
        otherClient.DefaultRequestHeaders.Add(
            RecommendationNotificationGatewayHeaders.UserId,
            "preference-owner-b");
        var otherResponse = await otherClient.GetAsync(
            RecommendationNotificationApiRoutes.NotificationPreferences);
        var other = await otherResponse.Content
            .ReadFromJsonAsync<NotificationPreferencesResponse>();

        Assert.Equal(HttpStatusCode.OK, defaultResponse.StatusCode);
        Assert.True(defaults!.PushEnabled);
        Assert.True(defaults.WebEnabled);
        Assert.Equal(NotificationTriggerCodes.All, defaults.EnabledNotificationTypes);
        Assert.Null(defaults.QuietHours);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.False(updated!.PushEnabled);
        Assert.True(updated.WebEnabled);
        Assert.Equal(
            new[]
            {
                NotificationTriggerCodes.BudgetExceeded,
                NotificationTriggerCodes.ScoreImproved
            },
            updated.EnabledNotificationTypes);
        Assert.Equal(quietHours, updated.QuietHours);
        Assert.Equal(updated, reread);
        Assert.True(other!.PushEnabled);
        Assert.True(other.WebEnabled);
        Assert.Equal(NotificationTriggerCodes.All, other.EnabledNotificationTypes);
        Assert.Null(other.QuietHours);
    }
}
