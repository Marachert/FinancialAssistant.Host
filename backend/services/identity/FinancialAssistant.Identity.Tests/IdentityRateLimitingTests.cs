using System.Net;
using System.Net.Http.Json;
using FinancialAssistant.Identity.Contracts.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace FinancialAssistant.Identity.Tests;

public sealed class IdentityRateLimitingTests
{
    [Fact]
    public async Task RegistrationLimit_ReturnsGeneric429WithoutAccountLeakage()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Identity:RateLimiting:Registration:PermitLimit"] = "1",
            ["Identity:RateLimiting:Registration:WindowSeconds"] = "120"
        });
        using var client = CreateClient(factory, "synthetic-identity-client-a");
        var email = "synthetic-rate-limit@example.invalid";
        var password = "Synthetic-Password-Not-A-Secret-123!";
        var request = new RegisterAccountRequest(
            email,
            password,
            new IdentityClientContext("synthetic-client-body-a", "web", "0.0-test"));

        using var first = await client.PostAsJsonAsync(IdentityApiRoutes.Register, request);
        using var second = await client.PostAsJsonAsync(IdentityApiRoutes.Register, request);
        var body = await second.Content.ReadAsStringAsync();
        var problem = await second.Content.ReadFromJsonAsync<IdentityApiErrorResponse>();

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(IdentityErrorCodes.RateLimited, problem.Code);
        Assert.Equal(429, problem.Status);
        Assert.True(problem.RetryAfterSeconds > 0);
        Assert.DoesNotContain(email, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(password, body, StringComparison.Ordinal);
        Assert.True(second.Headers.TryGetValues("Retry-After", out var values));
        Assert.Single(values);
    }

    [Fact]
    public async Task RateLimitPolicies_AreIndependentByOperation()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Identity:RateLimiting:Registration:PermitLimit"] = "1",
            ["Identity:RateLimiting:Registration:WindowSeconds"] = "120",
            ["Identity:RateLimiting:SignIn:PermitLimit"] = "1",
            ["Identity:RateLimiting:SignIn:WindowSeconds"] = "120"
        });
        using var client = CreateClient(factory, "synthetic-identity-client-b");
        var clientContext = new IdentityClientContext("synthetic-client-body-b", "android", "0.0-test");

        using var registration = await client.PostAsJsonAsync(
            IdentityApiRoutes.Register,
            new RegisterAccountRequest(
                "synthetic-registration@example.invalid",
                "Synthetic-Password-Not-A-Secret-123!",
                clientContext));
        using var registrationThrottled = await client.PostAsJsonAsync(
            IdentityApiRoutes.Register,
            new RegisterAccountRequest(
                "synthetic-registration-two@example.invalid",
                "Synthetic-Password-Not-A-Secret-123!",
                clientContext));
        using var signIn = await client.PostAsJsonAsync(
            IdentityApiRoutes.SignIn,
            new SignInRequest(
                "synthetic-registration@example.invalid",
                "Synthetic-Password-Not-A-Secret-123!",
                clientContext));

        Assert.NotEqual(HttpStatusCode.TooManyRequests, registration.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, registrationThrottled.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, signIn.StatusCode);
    }

    [Fact]
    public async Task ClientInstanceHeader_CreatesIndependentIdentityPartitions()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Identity:RateLimiting:SignIn:PermitLimit"] = "1",
            ["Identity:RateLimiting:SignIn:WindowSeconds"] = "120"
        });
        using var firstClient = CreateClient(factory, "synthetic-identity-partition-a");
        using var secondClient = CreateClient(factory, "synthetic-identity-partition-b");
        var request = new SignInRequest(
            "synthetic-unknown@example.invalid",
            "Synthetic-Password-Not-A-Secret-123!",
            new IdentityClientContext("synthetic-client-body-c", "ios", "0.0-test"));

        using var first = await firstClient.PostAsJsonAsync(IdentityApiRoutes.SignIn, request);
        using var throttled = await firstClient.PostAsJsonAsync(IdentityApiRoutes.SignIn, request);
        using var independent = await secondClient.PostAsJsonAsync(IdentityApiRoutes.SignIn, request);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, independent.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_IsNotRateLimited()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Identity:RateLimiting:Session:PermitLimit"] = "1",
            ["Identity:RateLimiting:Session:WindowSeconds"] = "120"
        });
        using var client = CreateClient(factory, "synthetic-identity-health");

        using var first = await client.GetAsync("/health");
        using var second = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IReadOnlyDictionary<string, string?> overrides)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(overrides));
        });
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> factory,
        string clientInstanceId)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Add("X-Client-Instance-Id", clientInstanceId);
        return client;
    }
}
