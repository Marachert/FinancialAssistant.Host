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
        Assert.Equal("monday", profile.FirstDayOfWeek);
        Assert.Equal(0m, profile.MonthlyBudgetAmount);
        Assert.False(profile.BudgetNotificationsEnabled);
        Assert.False(profile.WeeklySummaryNotificationsEnabled);
        Assert.False(profile.ProfileOnboardingCompleted);
        Assert.False(profile.PreferencesOnboardingCompleted);
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
                    Locale: "uk-UA",
                    TimeZone: "UTC",
                    CurrencyCode: "eur",
                    PrivacyMode: "strict",
                    AiPersonalizationEnabled: true,
                    FirstDayOfWeek: "sunday",
                    MonthlyBudgetAmount: 2500.50m,
                    BudgetNotificationsEnabled: true,
                    WeeklySummaryNotificationsEnabled: true,
                    ProfileOnboardingCompleted: true,
                    PreferencesOnboardingCompleted: true))
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
        Assert.Equal("sunday", updated.FirstDayOfWeek);
        Assert.Equal(2500.50m, updated.MonthlyBudgetAmount);
        Assert.True(updated.BudgetNotificationsEnabled);
        Assert.True(updated.WeeklySummaryNotificationsEnabled);
        Assert.True(updated.ProfileOnboardingCompleted);
        Assert.True(updated.PreferencesOnboardingCompleted);

        using var readOtherRequest = new HttpRequestMessage(HttpMethod.Get, ProfileApiRoutes.CurrentProfile);
        readOtherRequest.Headers.TryAddWithoutValidation(ProfileGatewayHeaders.UserId, "synthetic-user-b");
        var otherResponse = await client.SendAsync(readOtherRequest);
        var other = await otherResponse.Content.ReadFromJsonAsync<UserProfileResponse>();

        Assert.NotNull(other);
        Assert.Equal("USD", other.CurrencyCode);
        Assert.Equal("standard", other.PrivacyMode);
        Assert.False(other.AiPersonalizationEnabled);
        Assert.Equal("monday", other.FirstDayOfWeek);
        Assert.Equal(0m, other.MonthlyBudgetAmount);
        Assert.False(other.BudgetNotificationsEnabled);
        Assert.False(other.WeeklySummaryNotificationsEnabled);
        Assert.False(other.ProfileOnboardingCompleted);
        Assert.False(other.PreferencesOnboardingCompleted);
    }

    [Fact]
    public async Task CurrentProfile_WithoutGatewayUserContext_ReturnsUnauthorizedProblem()
    {
        var response = await client.GetAsync(ProfileApiRoutes.CurrentProfile);
        var problem = await response.Content.ReadFromJsonAsync<ProfileApiErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(problem);
        Assert.Equal("Authentication is required.", problem.Title);
        Assert.Equal(
            "Profile requests must be forwarded with a trusted gateway user context.",
            problem.Detail);
        Assert.Equal((int)HttpStatusCode.Unauthorized, problem.Status);
        Assert.Equal("authentication_required", problem.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.Type));
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
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
        Assert.Equal("Profile preferences are invalid.", problem.Title);
        Assert.Contains("Currency code", problem.Detail, StringComparison.Ordinal);
        Assert.Equal((int)HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("invalid_preferences", problem.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.Type));
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task InvalidTimeZone_ReturnsValidationProblem()
    {
        await CreateProfileAsync("synthetic-user-time-zone");

        using var updateRequest = new HttpRequestMessage(
            HttpMethod.Put,
            ProfileApiRoutes.CurrentProfilePreferences)
        {
            Content = JsonContent.Create(
                new UpdateUserPreferencesRequest(
                    "en-US",
                    "synthetic/not-a-time-zone",
                    "USD",
                    "standard",
                    false))
        };
        updateRequest.Headers.TryAddWithoutValidation(
            ProfileGatewayHeaders.UserId,
            "synthetic-user-time-zone");

        var response = await client.SendAsync(updateRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProfileApiErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("Profile preferences are invalid.", problem.Title);
        Assert.Contains("Time zone is invalid.", problem.Detail, StringComparison.Ordinal);
        Assert.Equal((int)HttpStatusCode.BadRequest, problem.Status);
        Assert.Equal("invalid_preferences", problem.Code);
        Assert.False(string.IsNullOrWhiteSpace(problem.TraceId));
    }

    [Fact]
    public async Task InvalidFirstDayOfWeek_ReturnsValidationProblem()
    {
        await CreateProfileAsync("synthetic-user-week-start");

        using var updateRequest = CreateUpdateRequest(
            "synthetic-user-week-start",
            new UpdateUserPreferencesRequest(
                "en-US",
                "UTC",
                "USD",
                "standard",
                false,
                FirstDayOfWeek: "not-a-weekday"));

        var response = await client.SendAsync(updateRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProfileApiErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("First day of week", problem.Detail, StringComparison.Ordinal);
        Assert.Equal("invalid_preferences", problem.Code);
    }

    [Fact]
    public async Task NegativeMonthlyBudget_ReturnsValidationProblem()
    {
        await CreateProfileAsync("synthetic-user-budget");

        using var updateRequest = CreateUpdateRequest(
            "synthetic-user-budget",
            new UpdateUserPreferencesRequest(
                "en-US",
                "UTC",
                "USD",
                "standard",
                false,
                MonthlyBudgetAmount: -0.01m));

        var response = await client.SendAsync(updateRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProfileApiErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("Monthly budget amount", problem.Detail, StringComparison.Ordinal);
        Assert.Equal("invalid_preferences", problem.Code);
    }

    private static HttpRequestMessage CreateUpdateRequest(
        string userId,
        UpdateUserPreferencesRequest preferences)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, ProfileApiRoutes.CurrentProfilePreferences)
        {
            Content = JsonContent.Create(preferences)
        };
        request.Headers.TryAddWithoutValidation(ProfileGatewayHeaders.UserId, userId);
        return request;
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
