using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Sessions;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Infrastructure.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Identity.Tests;

public sealed class IdentitySessionLifecycleTests : IClassFixture<IdentityContractWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IdentityContractWebApplicationFactory factory;
    private readonly HttpClient client;

    public IdentitySessionLifecycleTests(IdentityContractWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Refresh_RotatesSessionAndReuseRevokesEntireFamily()
    {
        var initial = await RegisterAsync();

        Assert.Equal(3, initial.AccessToken.Split('.').Length);
        Assert.StartsWith("rt1.", initial.RefreshToken, StringComparison.Ordinal);

        var currentBeforeRotation = await GetCurrentAsync(initial.AccessToken);
        Assert.Equal(HttpStatusCode.OK, currentBeforeRotation.StatusCode);
        var currentContext = await currentBeforeRotation.Content
            .ReadFromJsonAsync<CurrentUserContextResponse>(JsonOptions);
        Assert.NotNull(currentContext);
        Assert.Equal(initial.User.SessionId, currentContext.SessionId);

        var rotatedResponse = await client.PostAsJsonAsync(
            IdentityApiRoutes.Refresh,
            new RefreshSessionRequest(initial.RefreshToken, CreateClientContext("ios")));
        var rotated = await rotatedResponse.Content.ReadFromJsonAsync<AuthSessionResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, rotatedResponse.StatusCode);
        Assert.NotNull(rotated);
        Assert.NotEqual(initial.RefreshToken, rotated.RefreshToken);
        Assert.NotEqual(initial.AccessToken, rotated.AccessToken);
        Assert.NotEqual(initial.User.SessionId, rotated.User.SessionId);
        Assert.Equal(initial.User.UserId, rotated.User.UserId);

        var reuseResponse = await client.PostAsJsonAsync(
            IdentityApiRoutes.Refresh,
            new RefreshSessionRequest(initial.RefreshToken, CreateClientContext("ios")));
        var reuseProblem = await reuseResponse.Content
            .ReadFromJsonAsync<IdentityApiErrorResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
        Assert.NotNull(reuseProblem);
        Assert.Equal(IdentityErrorCodes.SessionRevoked, reuseProblem.Code);

        var familyResponse = await client.PostAsJsonAsync(
            IdentityApiRoutes.Refresh,
            new RefreshSessionRequest(rotated.RefreshToken, CreateClientContext("ios")));
        var familyProblem = await familyResponse.Content
            .ReadFromJsonAsync<IdentityApiErrorResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, familyResponse.StatusCode);
        Assert.NotNull(familyProblem);
        Assert.Equal(IdentityErrorCodes.SessionRevoked, familyProblem.Code);
        Assert.Contains(
            factory.EventPublisher.Publications,
            publication => publication.EventName == "token.revoked.v1"
                && publication.Data.TryGetValue("reason", out var reason)
                && reason == "refresh_reuse");
    }

    [Fact]
    public async Task Logout_RevokesSessionForCurrentContextAndRefresh()
    {
        var session = await RegisterAsync();
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, IdentityApiRoutes.Logout)
        {
            Content = JsonContent.Create(
                new LogoutRequest(session.RefreshToken, CreateClientContext("web")))
        };
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var logoutResponse = await client.SendAsync(logoutRequest);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var currentResponse = await GetCurrentAsync(session.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, currentResponse.StatusCode);

        var refreshResponse = await client.PostAsJsonAsync(
            IdentityApiRoutes.Refresh,
            new RefreshSessionRequest(session.RefreshToken, CreateClientContext("web")));
        var problem = await refreshResponse.Content
            .ReadFromJsonAsync<IdentityApiErrorResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(IdentityErrorCodes.SessionRevoked, problem.Code);
        Assert.Contains(
            factory.EventPublisher.Publications,
            publication => publication.EventName == "token.revoked.v1"
                && publication.Data.TryGetValue("reason", out var reason)
                && reason == "logout");
    }

    [Fact]
    public async Task PersistedSession_StoresOnlyHashOfRefreshValue()
    {
        var session = await RegisterAsync();
        var renewalService = factory.Services.GetRequiredService<IRefreshTokenService>();
        var store = factory.Services.GetRequiredService<IIdentitySessionStore>();

        Assert.True(renewalService.TryReadSessionId(session.RefreshToken, out var sessionId));
        var stored = await store.FindByIdAsync(sessionId);

        Assert.NotNull(stored);
        Assert.NotEqual(session.RefreshToken, stored.RefreshTokenHash);
        Assert.DoesNotContain(session.RefreshToken, stored.RefreshTokenHash, StringComparison.Ordinal);
        Assert.Equal(64, stored.RefreshTokenHash.Length);
    }

    [Fact]
    public async Task ExpiredSession_IsRejectedByAtomicRotationStore()
    {
        var store = new InMemoryIdentitySessionStore();
        var now = DateTimeOffset.UtcNow;
        var expired = new IdentitySessionRecord(
            Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"),
            "bb",
            "aa",
            "cc",
            IdentitySessionStatus.Active,
            "email_password",
            now.AddDays(-2),
            now.AddMinutes(-10),
            now.AddMinutes(-1));
        var replacement = expired with
        {
            Id = Guid.NewGuid().ToString("N"),
            Status = IdentitySessionStatus.Active,
            IssuedAtUtc = now,
            AccessTokenExpiresAtUtc = now.AddMinutes(15),
            RefreshTokenExpiresAtUtc = now.AddDays(30),
            RefreshTokenHash = "dd"
        };
        await store.TryCreateAsync(expired);

        var result = await store.RotateAsync(expired.Id, "aa", replacement, now);

        Assert.Equal(SessionRotationStoreResult.Expired, result);
        var stored = await store.FindByIdAsync(expired.Id);
        Assert.NotNull(stored);
        Assert.Equal(IdentitySessionStatus.Expired, stored.Status);
    }

    private async Task<AuthSessionResponse> RegisterAsync()
    {
        var response = await client.PostAsJsonAsync(
            IdentityApiRoutes.Register,
            new RegisterAccountRequest(
                $"session-{Guid.NewGuid():N}@example.invalid",
                "Synthetic-Only-Password-123!",
                CreateClientContext("android")));
        var session = await response.Content.ReadFromJsonAsync<AuthSessionResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(session);
        return session;
    }

    private async Task<HttpResponseMessage> GetCurrentAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, IdentityApiRoutes.CurrentUser);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await client.SendAsync(request);
    }

    private static IdentityClientContext CreateClientContext(string platform)
    {
        return new IdentityClientContext(
            $"synthetic-client-{Guid.NewGuid():N}",
            platform,
            "0.0-test");
    }
}
