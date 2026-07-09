using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.Profile.Contracts;

namespace FinancialAssistant.Profile.Tests;

public sealed class ProfileEndpointTests : IClassFixture<ProfileContractWebApplicationFactory>
{
    private readonly HttpClient client;

    public ProfileEndpointTests(ProfileContractWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task UserRegisteredEvent_CreatesDefaultProfileForGatewayUser()
    {
        var userId = "synthetic-user-001";
        var createdAt = new DateTimeOffset(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);

        var createResponse = await client.PostAsJsonAsync(
            ProfileApiRoutes.UserRegisteredEvent,
            new UserRegisteredProfileEvent(
                userId,
                createdAt,
                "synthetic-correlation",
                "synthetic-causation"));

        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, ProfileApiRoutes.CurrentProfile);
        request.Headers.TryAddWithoutValidation(ProfileGatewayHeaders.UserId, userId);

        var readResponse = await client.SendAsync(request);
        var profile = await readResponse.Content.ReadFromJsonAsync<UserProfileResponse>();

        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.NotNull(profile);
        Assert.Equal(userId, profile.UserId);
        Assert.Equal("en-US", profile.Locale);
        Assert.Equal("UTC", profile.TimeZone);
        Assert.Equal("USD", profile.CurrencyCode);
        Assert.Equal("standard", profile.PrivacyMode);
        Assert.False(profile.AiPersonalizationEnabled);
        Assert.Equal(createdAt, profile.CreatedAtUtc);
    }

    [Fact]
    public async Task UpdatePreferences_ChangesOnlyTheAuthenticatedUsersProfile()
    {
        await CreateProfileAsync("synthetic-user-a");
        await CreateProfileAsync("synthetic-user-b");

        using var updateRequest = new HttpRequestMessage(
            HttpMethod.Put,
            ProfileApiRoutes.CurrentProfilePreferences)
        {
            Content = JsonContent.Create(
                new UpdateUserPreferencesRequest(
                    "uk-UA",
                    "UTC",
                    "eur",
                    "strict",
                    true))
        };
        updateRequest.Headers.TryAddWithoutValidation(ProfileGatewayHeaders.UserId, "synthetic-user-a");

        var updateResponse = await client.SendAsync(updateRequest);
        var updated = await updateResponse.Content.ReadFromJsonAsync<UserProfileResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("synthetic-user-a", updated.UserId);
        Assert.Equal("uk-UA", updated.Locale);
        Assert.Equal("EUR", updated.CurrencyCode);
        Assert.Equal("strict", updated.PrivacyMode);
        Assert.True(updated.AiPersonalizationEnabled);

        using var readOtherRequest = new HttpRequestMessage(HttpMethod.Get, ProfileApiRoutes.CurrentProfile);
        readOtherRequest.Headers.TryAddWithoutValidation(ProfileGatewayHeaders.UserId, "synthetic-user-b");
        var otherResponse = await client.SendAsync(readOtherRequest);
        var other = await otherResponse.Content.ReadFromJsonAsync<UserProfileResponse>();

        Assert.NotNull(other);
        Assert.Equal("USD", other.CurrencyCode);
        Assert.Equal("standard", other.PrivacyMode);
        Assert.False(other.AiPersonalizationEnabled);
    }

    [Fact]
    public async Task CurrentProfile_WithoutGatewayUserContext_ReturnsUnauthorizedProblem()
    {
        var response = await client.GetAsync(ProfileApiRoutes.CurrentProfile);
        var problem = await response.Content.ReadFromJsonAsync<ProfileApiErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problem);
        Assert.Equal("authentication_required", problem.Code);
    }

    [Fact]
    public async Task InvalidPreferences_ReturnValidationProblem()
    {
        await CreateProfileAsync("synthetic-user-validation");

        using var updateRequest = new HttpRequestMessage(
            HttpMethod.Put,
            ProfileApiRoutes.CurrentProfilePreferences)
        {
            Content = JsonContent.Create(
                new UpdateUserPreferencesRequest(
                    "en-US",
                    "UTC",
                    "US",
                    "standard",
                    false))
        };
        updateRequest.Headers.TryAddWithoutValidation(ProfileGatewayHeaders.UserId, "synthetic-user-validation");

        var response = await client.SendAsync(updateRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProfileApiErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("invalid_preferences", problem.Code);
    }

    private async Task CreateProfileAsync(string userId)
    {
        var response = await client.PostAsJsonAsync(
            ProfileApiRoutes.UserRegisteredEvent,
            new UserRegisteredProfileEvent(
                userId,
                DateTimeOffset.UtcNow,
                "synthetic-correlation",
                "synthetic-causation"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
