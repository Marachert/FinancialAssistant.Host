using System.Diagnostics;
using System.Text.Json;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Phone;
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

        var identityOptions = configuration
            .GetSection(IdentityServiceOptions.SectionName)
            .Get<IdentityServiceOptions>() ?? new IdentityServiceOptions();
        var keyMaterial = new IdentityJwtKeyMaterial(identityOptions.Authentication);
        var phoneOptions = identityOptions.Providers.Phone;
        var phonePolicy = new PhoneVerificationPolicy(
            TimeSpan.FromMinutes(phoneOptions.ChallengeLifetimeMinutes),
            TimeSpan.FromSeconds(phoneOptions.ResendCooldownSeconds),
            TimeSpan.FromMinutes(phoneOptions.StartWindowMinutes),
            phoneOptions.MaximumAttempts,
            phoneOptions.MaximumStartsPerPhone,
            phoneOptions.MaximumStartsPerClient,
            phoneOptions.CodeLength);

        services.AddSingleton(keyMaterial);
        services.AddSingleton(phonePolicy);
        services.AddSingleton<InMemoryIdentityAccountStore>();
        services.AddSingleton<IIdentityAccountStore>(provider =>
            provider.GetRequiredService<InMemoryIdentityAccountStore>());
        services.AddSingleton<IIdentityFederatedAccountStore>(provider =>
            provider.GetRequiredService<InMemoryIdentityAccountStore>());
        services.AddSingleton<IIdentitySessionStore, InMemoryIdentitySessionStore>();
        services.AddSingleton<IPhoneVerificationChallengeStore, InMemoryPhoneVerificationChallengeStore>();
        services.AddSingleton<IPhoneVerificationProvider, DisabledPhoneVerificationProvider>();
        services.AddSingleton<IEmailLookupHasher, HmacEmailLookupHasher>();
        services.AddSingleton<IIdentityProviderIdentifierHasher, HmacIdentityProviderIdentifierHasher>();
        services.AddSingleton<IGoogleIdentityTokenValidator, GoogleIdentityTokenValidator>();
        services.AddSingleton<IAppleIdentityTokenValidator, AppleIdentityTokenValidator>();
        services.AddSingleton<IPasswordCredentialHasher, AspNetCorePasswordCredentialHasher>();
        services.AddSingleton<IRefreshTokenService, OpaqueRefreshTokenService>();
        services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();
        services.AddSingleton<ISessionLifetimePolicy, SessionLifetimePolicy>();
        services.AddScoped<IInitialSessionIssuer, OpaqueInitialSessionIssuer>();
        services.AddSingleton<ISystemClock, SystemClock>();

        services.AddSingleton<IIdentityEventOutbox, InMemoryIdentityEventOutbox>();
        services.AddSingleton<IIdentityEventSubjectHasher, HmacIdentityEventSubjectHasher>();
        services.AddSingleton<IIdentityEventPublisher, OutboxIdentityEventPublisher>();
        if (string.Equals(identityOptions.Events.Mode, "RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IIdentityEventTransport, RabbitMqIdentityEventTransport>();
        }
        else
        {
            services.AddSingleton<InMemoryIdentityEventTransport>();
            services.AddSingleton<IIdentityEventTransport>(provider =>
                provider.GetRequiredService<InMemoryIdentityEventTransport>());
        }

        services.AddHostedService<IdentityOutboxDispatcher>();
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
