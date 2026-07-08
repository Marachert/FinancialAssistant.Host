using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Phone;
using FinancialAssistant.Identity.Application.Providers.Apple;
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
        services.AddScoped<IAppleProviderAuthenticationService, AppleProviderAuthenticationService>();
        services.AddScoped<IPhoneVerificationAuthenticationService, PhoneVerificationAuthenticationService>();
        services.AddScoped<IIdentitySessionService, IdentitySessionService>();
        return services;
    }
}
