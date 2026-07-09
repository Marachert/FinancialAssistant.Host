using FinancialAssistant.Profile.Application.Abstractions;
using FinancialAssistant.Profile.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Profile.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddProfileInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IProfileStore, InMemoryProfileStore>();
        return services;
    }
}
