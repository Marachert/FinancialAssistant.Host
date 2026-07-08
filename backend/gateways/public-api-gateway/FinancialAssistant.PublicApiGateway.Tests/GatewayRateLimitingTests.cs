using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinancialAssistant.PublicApiGateway.RateLimiting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class GatewayRateLimitingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SignInLimit_ReturnsSafe429WithoutCredentialLeakage()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Gateway:RateLimiting:Policies:identity-sign-in:PermitLimit"] = "1",
            ["Gateway:RateLimiting:Policies:identity-sign-in:WindowSeconds"] = "120"
        });
        using var client = CreateClient(factory, "synthetic-client-rate-limit-a");
        var payload = new
        {
            email = "synthetic-victim@example.invalid",
            password = "Synthetic-Password-Not-A-Secret-123!"
        };

        using var first = await client.PostAsJsonAsync("/auth/v1/sign-in", payload);
        using var second = await client.PostAsJsonAsync("/auth/v1/sign-in", payload);
        var body = await second.Content.ReadAsStringAsync();
        var problem = JsonSerializer.Deserialize<RateLimitProblem>(body, JsonOptions);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal("rate_limited", problem.Code);
        Assert.Equal(429, problem.Status);
        Assert.True(problem.RetryAfterSeconds > 0);
        Assert.DoesNotContain(payload.email, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(payload.password, body, StringComparison.Ordinal);
        Assert.True(second.Headers.TryGetValues("Retry-After", out var values));
        Assert.Single(values);
    }

    [Fact]
    public async Task ChangingClientInstanceHeader_DoesNotResetIpWidePartition()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Gateway:RateLimiting:Policies:identity-sign-in:PermitLimit"] = "1",
            ["Gateway:RateLimiting:Policies:identity-sign-in:WindowSeconds"] = "120"
        });
        using var firstClient = CreateClient(factory, "synthetic-client-partition-a");
        using var spoofedClient = CreateClient(factory, "synthetic-client-partition-b");

        using var first = await firstClient.PostAsync("/auth/v1/sign-in", null);
        using var throttled = await spoofedClient.PostAsync("/auth/v1/sign-in", null);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
    }

    [Fact]
    public async Task PartitionCache_IsBoundedAndOverflowDoesNotCreateFreshBuckets()
    {
        var options = Options.Create(
            new GatewayRateLimitOptions
            {
                MaximumPartitionCount = 1,
                PartitionIdleExpirationSeconds = 60
            });
        using var limiter = new GatewayRateLimiter(options);
        var policy = new GatewayRateLimitPolicyOptions
        {
            PermitLimit = 1,
            WindowSeconds = 120
        };

        var cached = await limiter.AcquireAsync("general", "general:cached", policy, CancellationToken.None);
        var overflowFirst = await limiter.AcquireAsync("general", "general:overflow-a", policy, CancellationToken.None);
        var overflowSecond = await limiter.AcquireAsync("general", "general:overflow-b", policy, CancellationToken.None);

        Assert.True(cached.IsAcquired);
        Assert.True(overflowFirst.IsAcquired);
        Assert.False(overflowSecond.IsAcquired);
        Assert.InRange(limiter.CachedPartitionCount, 0, 1);
    }

    [Fact]
    public async Task HealthEndpoints_AreExcludedFromRateLimiting()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Gateway:RateLimiting:Policies:general:PermitLimit"] = "1",
            ["Gateway:RateLimiting:Policies:general:WindowSeconds"] = "120"
        });
        using var client = CreateClient(factory, "synthetic-client-health-check");

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

    private sealed record RateLimitProblem(
        string Code,
        int Status,
        int RetryAfterSeconds);
}
