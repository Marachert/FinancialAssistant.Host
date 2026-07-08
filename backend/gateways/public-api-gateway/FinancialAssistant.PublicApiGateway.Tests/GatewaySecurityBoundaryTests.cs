using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using FinancialAssistant.PublicApiGateway.Observability;
using FinancialAssistant.PublicApiGateway.Routing;
using FinancialAssistant.PublicApiGateway.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class GatewaySecurityBoundaryTests
{
    private const string SigningKey = "synthetic-gateway-signing-key-with-at-least-32-bytes";
    private const string Issuer = "financial-assistant-identity";
    private const string Audience = "financial-assistant-clients";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PublicAllowlist_AllowsExactMethodAndPathWithoutAccessToken()
    {
        var options = CreateOptions(
            publicEndpoints:
            [
                new GatewayPublicEndpointDefinition
                {
                    Method = HttpMethods.Post,
                    Path = "/auth/v1/sign-in"
                }
            ]);
        var boundary = CreateBoundary(options);
        var context = CreateContext(HttpMethods.Post, "/auth/v1/sign-in");

        var authorized = await boundary.AuthorizeAsync(context, CreateRoute(GatewayAccessPolicies.Authenticated));

        Assert.True(authorized);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Null(GatewayUserContext.Get(context));
    }

    [Fact]
    public async Task PublicAllowlist_DoesNotAllowDifferentMethodOrPath()
    {
        var options = CreateOptions(
            publicEndpoints:
            [
                new GatewayPublicEndpointDefinition
                {
                    Method = HttpMethods.Post,
                    Path = "/auth/v1/sign-in"
                }
            ]);
        var boundary = CreateBoundary(options);
        var context = CreateContext(HttpMethods.Get, "/auth/v1/sign-in");

        var authorized = await boundary.AuthorizeAsync(context, CreateRoute(GatewayAccessPolicies.Authenticated));
        var problem = await ReadProblemAsync(context);

        Assert.False(authorized);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("authentication_required", problem.Code);
    }

    [Fact]
    public async Task AuthenticatedRoute_WithoutToken_ReturnsSafe401()
    {
        var options = CreateOptions();
        var boundary = CreateBoundary(options);
        var context = CreateContext(HttpMethods.Get, "/users/me");

        var authorized = await boundary.AuthorizeAsync(context, CreateRoute(GatewayAccessPolicies.Authenticated));
        var body = await ReadBodyAsync(context);
        var problem = JsonSerializer.Deserialize<GatewayProblem>(body, JsonOptions);

        Assert.False(authorized);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("Bearer", context.Response.Headers.WWWAuthenticate);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.NotNull(problem);
        Assert.Equal("authentication_required", problem.Code);
        Assert.Equal("synthetic-correlation-id", problem.CorrelationId);
        Assert.DoesNotContain("auth-service", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticatedRoute_WithValidToken_SetsSafeUserContext()
    {
        var options = CreateOptions();
        var boundary = CreateBoundary(options);
        var context = CreateContext(HttpMethods.Get, "/users/me");
        context.Request.Headers.Authorization = $"Bearer {CreateToken(["user"])}";

        var authorized = await boundary.AuthorizeAsync(context, CreateRoute(GatewayAccessPolicies.Authenticated));
        var userContext = GatewayUserContext.Get(context);

        Assert.True(authorized);
        Assert.NotNull(userContext);
        Assert.Equal("user-123", userContext.UserId);
        Assert.Equal("session-456", userContext.SessionId);
        Assert.Equal(new[] { "user" }, userContext.Roles);
    }

    [Fact]
    public async Task AuthenticatedRoute_WithExpiredToken_ReturnsSessionExpired()
    {
        var options = CreateOptions(clockSkewSeconds: 0);
        var boundary = CreateBoundary(options);
        var context = CreateContext(HttpMethods.Get, "/users/me");
        context.Request.Headers.Authorization = $"Bearer {CreateToken(["user"], expiresAtUtc: DateTime.UtcNow.AddMinutes(-1))}";

        var authorized = await boundary.AuthorizeAsync(context, CreateRoute(GatewayAccessPolicies.Authenticated));
        var problem = await ReadProblemAsync(context);

        Assert.False(authorized);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("session_expired", problem.Code);
        Assert.Null(GatewayUserContext.Get(context));
    }

    [Fact]
    public async Task AuthenticatedRoute_WithInvalidSignature_ReturnsGenericSessionInvalid()
    {
        var options = CreateOptions();
        var boundary = CreateBoundary(options);
        var context = CreateContext(HttpMethods.Get, "/users/me");
        context.Request.Headers.Authorization = $"Bearer {CreateToken(["user"], signingKey: "different-synthetic-signing-key-with-at-least-32-bytes")}";

        var authorized = await boundary.AuthorizeAsync(context, CreateRoute(GatewayAccessPolicies.Authenticated));
        var body = await ReadBodyAsync(context);
        var problem = JsonSerializer.Deserialize<GatewayProblem>(body, JsonOptions);

        Assert.False(authorized);
        Assert.NotNull(problem);
        Assert.Equal("session_invalid", problem.Code);
        Assert.DoesNotContain("different-synthetic", body, StringComparison.Ordinal);
        Assert.DoesNotContain("signature", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminRoute_WithUserRole_ReturnsSafe403()
    {
        var options = CreateOptions();
        var boundary = CreateBoundary(options);
        var context = CreateContext(HttpMethods.Get, "/admin/monitoring");
        context.Request.Headers.Authorization = $"Bearer {CreateToken(["user"])}";

        var authorized = await boundary.AuthorizeAsync(context, CreateRoute(GatewayAccessPolicies.Admin));
        var problem = await ReadProblemAsync(context);

        Assert.False(authorized);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("forbidden", problem.Code);
        Assert.False(context.Response.Headers.ContainsKey("WWW-Authenticate"));
    }

    [Fact]
    public async Task AdminRoute_WithAdminRole_IsAuthorized()
    {
        var options = CreateOptions();
        var boundary = CreateBoundary(options);
        var context = CreateContext(HttpMethods.Get, "/admin/monitoring");
        context.Request.Headers.Authorization = $"Bearer {CreateToken(["user", "admin"])}";

        var authorized = await boundary.AuthorizeAsync(context, CreateRoute(GatewayAccessPolicies.Admin));

        Assert.True(authorized);
        Assert.True(GatewayUserContext.Get(context)?.IsInRole("admin"));
    }

    [Fact]
    public void EnforceMode_WithShortSigningKey_FailsConfigurationValidation()
    {
        var options = CreateOptions(signingKey: "too-short");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new GatewayAccessTokenValidator(Options.Create(options)));

        Assert.Contains("at least 32", exception.Message, StringComparison.Ordinal);
    }

    private static GatewaySecurityBoundary CreateBoundary(GatewaySecurityOptions options)
    {
        var optionsWrapper = Options.Create(options);
        return new GatewaySecurityBoundary(
            optionsWrapper,
            new GatewayAccessTokenValidator(optionsWrapper),
            NullLogger<GatewaySecurityBoundary>.Instance);
    }

    private static GatewaySecurityOptions CreateOptions(
        GatewayPublicEndpointDefinition[]? publicEndpoints = null,
        int clockSkewSeconds = 30,
        string signingKey = SigningKey) =>
        new()
        {
            Mode = GatewaySecurityModes.Enforce,
            AuthenticationHeaderName = "Authorization",
            AccessTokenSigningKey = signingKey,
            AccessTokenIssuer = Issuer,
            AccessTokenAudience = Audience,
            ClockSkewSeconds = clockSkewSeconds,
            AdminRole = GatewayRoles.Admin,
            IncludePolicyHeaders = true,
            PublicEndpoints = publicEndpoints ?? []
        };

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.Items[CorrelationHeaders.ContextItemKey] = "synthetic-correlation-id";
        return context;
    }

    private static GatewayRouteDefinition CreateRoute(string accessPolicy) =>
        new()
        {
            RouteKey = "synthetic-route",
            PublicPattern = "/synthetic",
            ServiceOwner = "Synthetic Service",
            InternalDestination = "synthetic-service",
            AccessPolicy = accessPolicy,
            Status = GatewayRouteStatuses.Active,
            Methods = [HttpMethods.Get, HttpMethods.Post]
        };

    private static string CreateToken(
        string[] roles,
        DateTime? expiresAtUtc = null,
        string signingKey = SigningKey)
    {
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, "user-123"),
            new("sid", "session-456"),
            new("amr", "email_password")
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now.AddMinutes(-5),
            IssuedAt = now.AddMinutes(-5),
            Expires = expiresAtUtc ?? now.AddMinutes(10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static async Task<GatewayProblem> ReadProblemAsync(HttpContext context)
    {
        var body = await ReadBodyAsync(context);
        return Assert.IsType<GatewayProblem>(JsonSerializer.Deserialize<GatewayProblem>(body, JsonOptions));
    }

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private sealed record GatewayProblem(string Code, string? CorrelationId);
}
