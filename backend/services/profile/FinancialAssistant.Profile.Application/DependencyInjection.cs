using FinancialAssistant.Profile.Application.Abstractions;
using FinancialAssistant.Profile.Application.Profiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialAssistant.Profile.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddProfileApplication(this IServiceCollection services)
    {
        services.TryAddSingleton<IProfileClock, SystemProfileClock>();
        services.AddSingleton<IUserProfileService, UserProfileService>();
        return services;
    }
}
