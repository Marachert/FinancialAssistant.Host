using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Providers.Google;
using FinancialAssistant.Identity.Application.Sessions;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<IIdentityAuthenticationService, IdentityAuthenticationService>();
        services.AddScoped<IGoogleProviderAuthenticationService, GoogleProviderAuthenticationService>();
        services.AddScoped<IIdentitySessionService, IdentitySessionService>();
        return services;
    }
}
