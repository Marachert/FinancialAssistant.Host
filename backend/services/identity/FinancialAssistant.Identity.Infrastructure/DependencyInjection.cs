using System.Diagnostics;
using System.Text.Json;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Infrastructure.Authentication;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using FinancialAssistant.Identity.Infrastructure.Health;
using FinancialAssistant.Identity.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Identity.Infrastructure;

public static class DependencyInjection
{
    private const string ProblemJson = "application/problem+json";
    private static readonly JsonSerializerOptions ProblemJsonOptions = new(JsonSerializerDefaults.Web);

    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<IdentityServiceOptions>(
            configuration.GetSection(IdentityServiceOptions.SectionName));

        var authentication = configuration
            .GetSection($"{IdentityServiceOptions.SectionName}:Authentication")
            .Get<IdentityAuthenticationOptions>() ?? new IdentityAuthenticationOptions();
        var keyMaterial = new IdentityJwtKeyMaterial(authentication);

        services.AddSingleton(keyMaterial);
        services.AddSingleton<IIdentityAccountStore, InMemoryIdentityAccountStore>();
        services.AddSingleton<IIdentitySessionStore, InMemoryIdentitySessionStore>();
        services.AddSingleton<IEmailLookupHasher, HmacEmailLookupHasher>();
        services.AddSingleton<IPasswordCredentialHasher, AspNetCorePasswordCredentialHasher>();
        services.AddSingleton<IRefreshTokenService, OpaqueRefreshTokenService>();
        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
        services.AddSingleton<ISessionLifetimePolicy, SessionLifetimePolicy>();
        services.AddScoped<IInitialSessionIssuer, OpaqueInitialSessionIssuer>();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IIdentityEventPublisher, NoOpIdentityEventPublisher>();
        services.AddSingleton<IdentityReadinessHealthCheck>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = keyMaterial.CreateValidationParameters();
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = WriteIdentityChallengeAsync
                };
            });
        services.AddAuthorization();

        return services;
    }

    private static async Task WriteIdentityChallengeAsync(JwtBearerChallengeContext context)
    {
        context.HandleResponse();

        var httpContext = context.HttpContext;
        var suppliedCorrelationId = httpContext.Request.Headers[IdentityApiHeaders.CorrelationId].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(suppliedCorrelationId)
            ? Activity.Current?.Id ?? httpContext.TraceIdentifier
            : suppliedCorrelationId;
        var response = new IdentityApiErrorResponse(
            "https://errors.financial-assistant.app/identity/session-invalid",
            "Session operation failed.",
            StatusCodes.Status401Unauthorized,
            IdentityErrorCodes.SessionInvalid,
            "The current session is not valid.",
            correlationId);

        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        httpContext.Response.ContentType = ProblemJson;
        httpContext.Response.Headers.WWWAuthenticate = JwtBearerDefaults.AuthenticationScheme;
        await JsonSerializer.SerializeAsync(
            httpContext.Response.Body,
            response,
            ProblemJsonOptions,
            httpContext.RequestAborted);
    }
}
