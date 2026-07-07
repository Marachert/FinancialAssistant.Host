using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Providers.Google;
using FinancialAssistant.Identity.Contracts.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Identity.Tests;

public sealed class GoogleSignInTests : IClassFixture<IdentityContractWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IdentityContractWebApplicationFactory factory;
    private readonly HttpClient client;

    public GoogleSignInTests(IdentityContractWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task ValidGoogleToken_CreatesProviderAccountAndSession()
    {
        var token = CreateToken();
        var subject = $"google-subject-{Guid.NewGuid():N}";
        factory.GoogleTokenValidator.SetResult(
            token,
            GoogleIdentityTokenValidationResult.Valid(
                new GoogleIdentityPrincipal(subject, CreateEmail(), true, null)));

        var response = await client.PostAsJsonAsync(
            IdentityApiRoutes.GoogleSignIn,
            new GoogleSignInRequest(token, CreateClient("android")));
        var session = await response.Content.ReadFromJsonAsync<AuthSessionResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(session);
        Assert.Equal("google_oidc", session.User.AuthenticationMethod);
        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken));
        Assert.DoesNotContain(token, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var identifierHasher = factory.Services.GetRequiredService<IIdentityProviderIdentifierHasher>();
        var providerStore = factory.Services.GetRequiredService<IIdentityFederatedAccountStore>();
        var subjectHash = identifierHasher.Hash("google", "subject", subject);
        var providerLink = await providerStore.FindProviderLinkAsync("google", subjectHash);
        Assert.NotNull(providerLink);
        Assert.Equal(session.User.UserId, providerLink.AccountId);
        Assert.NotEqual(subject, providerLink.ProviderSubjectHash);
        Assert.Contains(
            factory.EventPublisher.Publications,
            publication => publication.EventName == "user.registered.v1"
                && publication.SubjectId == session.User.UserId
                && publication.Data["authenticationMethod"] == "google_oidc");
    }

    [Fact]
    public async Task RepeatedGoogleToken_ReusesLinkedAccount()
    {
        var token = CreateToken();
        var subject = $"google-subject-{Guid.NewGuid():N}";
        factory.GoogleTokenValidator.SetResult(
            token,
            GoogleIdentityTokenValidationResult.Valid(
                new GoogleIdentityPrincipal(subject, CreateEmail(), true, null)));

        var first = await client.PostAsJsonAsync(
            IdentityApiRoutes.GoogleSignIn,
            new GoogleSignInRequest(token, CreateClient("ios")));
        var firstSession = await first.Content.ReadFromJsonAsync<AuthSessionResponse>(JsonOptions);
        var second = await client.PostAsJsonAsync(
            IdentityApiRoutes.GoogleSignIn,
            new GoogleSignInRequest(token, CreateClient("ios")));
        var secondSession = await second.Content.ReadFromJsonAsync<AuthSessionResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotNull(firstSession);
        Assert.NotNull(secondSession);
        Assert.Equal(firstSession.User.UserId, secondSession.User.UserId);
        Assert.NotEqual(firstSession.User.SessionId, secondSession.User.SessionId);
    }

    [Fact]
    public async Task VerifiedEmailMatchingLocalCredential_RequiresExplicitLink()
    {
        var email = CreateEmail();
        const string password = "Synthetic-Only-Password-123!";
        var registration = await client.PostAsJsonAsync(
            IdentityApiRoutes.Register,
            new RegisterAccountRequest(email, password, CreateClient("web")));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        var token = CreateToken();
        factory.GoogleTokenValidator.SetResult(
            token,
            GoogleIdentityTokenValidationResult.Valid(
                new GoogleIdentityPrincipal(
                    $"google-subject-{Guid.NewGuid():N}",
                    email,
                    true,
                    null)));

        var response = await client.PostAsJsonAsync(
            IdentityApiRoutes.GoogleSignIn,
            new GoogleSignInRequest(token, CreateClient("web")));
        var problem = await response.Content.ReadFromJsonAsync<IdentityApiErrorResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(IdentityErrorCodes.ProviderLinkRequired, problem.Code);
        Assert.DoesNotContain(email, await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidGoogleToken_ReturnsSafeAuthenticationFailure()
    {
        var token = CreateToken();
        factory.GoogleTokenValidator.SetResult(token, GoogleIdentityTokenValidationResult.Invalid());

        var response = await client.PostAsJsonAsync(
            IdentityApiRoutes.GoogleSignIn,
            new GoogleSignInRequest(token, CreateClient("android")));
        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<IdentityApiErrorResponse>(body, JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(IdentityErrorCodes.ProviderAuthenticationFailed, problem.Code);
        Assert.DoesNotContain(token, body, StringComparison.Ordinal);
        Assert.Contains(
            factory.EventPublisher.Publications,
            publication => publication.EventName == "authentication.failed.v1"
                && publication.SubjectId is null
                && publication.Data["authenticationMethod"] == "google_oidc");
    }

    [Fact]
    public async Task GoogleProviderUnavailable_ReturnsSafeServiceUnavailable()
    {
        var token = CreateToken();
        factory.GoogleTokenValidator.SetResult(token, GoogleIdentityTokenValidationResult.Unavailable());

        var response = await client.PostAsJsonAsync(
            IdentityApiRoutes.GoogleSignIn,
            new GoogleSignInRequest(token, CreateClient("android")));
        var problem = await response.Content.ReadFromJsonAsync<IdentityApiErrorResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(IdentityErrorCodes.ProviderUnavailable, problem.Code);
    }

    [Fact]
    public async Task InvalidGoogleRequest_ReturnsValidationErrors()
    {
        var response = await client.PostAsJsonAsync(
            IdentityApiRoutes.GoogleSignIn,
            new GoogleSignInRequest("short", new IdentityClientContext("short", "desktop", null)));
        var problem = await response.Content.ReadFromJsonAsync<IdentityApiErrorResponse>(JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(IdentityErrorCodes.ValidationFailed, problem.Code);
        Assert.NotNull(problem.Errors);
        Assert.Contains("idToken", problem.Errors.Keys);
        Assert.Contains("client.clientInstanceId", problem.Errors.Keys);
        Assert.Contains("client.platform", problem.Errors.Keys);
    }

    private static IdentityClientContext CreateClient(string platform) =>
        new($"synthetic-client-{Guid.NewGuid():N}", platform, "0.0-test");

    private static string CreateEmail() =>
        $"synthetic-{Guid.NewGuid():N}@example.invalid";

    private static string CreateToken() =>
        $"synthetic-google-token-{Guid.NewGuid():N}-{new string('x', 96)}";
}
