using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Income.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIncomeApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IIncomeManagementService, IncomeManagementService>();
        return services;
    }
}
