using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Tests;

public sealed class IdentityContractEndpointTests : IClassFixture<IdentityContractWebApplicationFactory>
{
    private const string SyntheticCorrelationId = "synthetic-correlation-fin-76-auth-challenge";
    private readonly HttpClient client;

    public IdentityContractEndpointTests(IdentityContractWebApplicationFactory factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenApi_ContainsAllVersionedIdentityRoutesAndSchemas()
    {
        var openApi = await client.GetStringAsync("/openapi/v1.json");
        var routes = new[]
        {
            IdentityApiRoutes.Register,
            IdentityApiRoutes.SignIn,
            IdentityApiRoutes.Refresh,
            IdentityApiRoutes.Logout,
            IdentityApiRoutes.CurrentUser
        };

        foreach (var route in routes)
        {
            Assert.Contains($"\"{route}\"", openApi, StringComparison.Ordinal);
        }

        Assert.Contains(nameof(RegisterAccountRequest), openApi, StringComparison.Ordinal);
        Assert.Contains(nameof(SignInRequest), openApi, StringComparison.Ordinal);
        Assert.Contains(nameof(RefreshSessionRequest), openApi, StringComparison.Ordinal);
        Assert.Contains(nameof(LogoutRequest), openApi, StringComparison.Ordinal);
        Assert.Contains(nameof(AuthSessionResponse), openApi, StringComparison.Ordinal);
        Assert.Contains(nameof(CurrentUserContextResponse), openApi, StringComparison.Ordinal);
        Assert.Contains(nameof(IdentityApiErrorResponse), openApi, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MalformedRefresh_ReturnsValidationProblem()
    {
        var response = await client.PostAsJsonAsync(
            IdentityApiRoutes.Refresh,
            new RefreshSessionRequest(
                "short",
                new IdentityClientContext("synthetic-client-001", "ios", "0.0-test")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(IdentityApiRoutes.Logout, "POST", false)]
    [InlineData(IdentityApiRoutes.Logout, "POST", true)]
    [InlineData(IdentityApiRoutes.CurrentUser, "GET", false)]
    [InlineData(IdentityApiRoutes.CurrentUser, "GET", true)]
    public async Task ProtectedSessionContracts_ReturnIdentityProblemForAuthenticationChallenges(
        string route,
        string method,
        bool includeInvalidBearer)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        request.Headers.TryAddWithoutValidation(IdentityApiHeaders.CorrelationId, SyntheticCorrelationId);
        if (includeInvalidBearer)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                "synthetic.invalid.access-token");
        }

        if (method == "POST")
        {
            request.Content = JsonContent.Create(
                new LogoutRequest(
                    "synthetic-refresh-token-value-that-is-not-real-000001",
                    new IdentityClientContext("synthetic-client-001", "web", "0.0-test")));
        }

        var response = await client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<IdentityApiErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
        Assert.NotNull(problem);
        Assert.Equal(IdentityErrorCodes.SessionInvalid, problem.Code);
        Assert.Equal(SyntheticCorrelationId, problem.TraceId);
    }
}
