using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Infrastructure.Authentication;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using FinancialAssistant.Identity.Infrastructure.Health;
using FinancialAssistant.Identity.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Identity.Infrastructure;

public static class DependencyInjection
{
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
            });
        services.AddAuthorization();

        return services;
    }
}
