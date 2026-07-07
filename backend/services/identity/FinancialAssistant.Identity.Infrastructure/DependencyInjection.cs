using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using FinancialAssistant.Identity.Infrastructure.Health;
using FinancialAssistant.Identity.Infrastructure.Messaging;
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
        services.AddSingleton<IIdentityEventPublisher, NoOpIdentityEventPublisher>();
        services.AddSingleton<IdentityReadinessHealthCheck>();

        return services;
    }
}
