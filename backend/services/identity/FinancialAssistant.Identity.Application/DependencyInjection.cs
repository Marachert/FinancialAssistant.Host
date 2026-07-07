using FinancialAssistant.Identity.Application.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<IIdentityAuthenticationService, IdentityAuthenticationService>();
        return services;
    }
}
