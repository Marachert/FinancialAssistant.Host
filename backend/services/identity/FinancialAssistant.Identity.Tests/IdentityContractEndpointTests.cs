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

    [Theory]
    [InlineData(IdentityApiRoutes.Refresh)]
    [InlineData(IdentityApiRoutes.Logout)]
    public async Task Fin76PostContracts_RemainExplicitPlaceholders(string route)
    {
        object request = route switch
        {
            IdentityApiRoutes.Refresh => new RefreshSessionRequest(
                "synthetic-refresh-token-value-that-is-not-real-000001",
                new IdentityClientContext("synthetic-client-001", "ios", "0.0-test")),
            IdentityApiRoutes.Logout => new LogoutRequest(
                "synthetic-refresh-token-value-that-is-not-real-000001",
                new IdentityClientContext("synthetic-client-001", "web", "0.0-test")),
            _ => throw new InvalidOperationException("Unexpected route.")
        };

        var response = await client.PostAsJsonAsync(route, request);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task CurrentUserContract_RemainsExplicitPlaceholder()
    {
        var response = await client.GetAsync(IdentityApiRoutes.CurrentUser);

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }
}
