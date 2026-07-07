using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        return services;
    }
}
