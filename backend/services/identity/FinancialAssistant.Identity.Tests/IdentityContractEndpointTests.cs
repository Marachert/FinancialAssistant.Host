using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Tests;

public sealed class IdentityContractEndpointTests : IClassFixture<IdentityContractWebApplicationFactory>
{
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
    [InlineData(IdentityApiRoutes.Logout, "POST")]
    [InlineData(IdentityApiRoutes.CurrentUser, "GET")]
    public async Task ProtectedSessionContracts_RejectAnonymousRequests(string route, string method)
    {
        HttpResponseMessage response;
        if (method == "POST")
        {
            response = await client.PostAsJsonAsync(
                route,
                new LogoutRequest(
                    "synthetic-refresh-token-value-that-is-not-real-000001",
                    new IdentityClientContext("synthetic-client-001", "web", "0.0-test")));
        }
        else
        {
            response = await client.GetAsync(route);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
