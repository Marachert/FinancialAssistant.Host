using FinancialAssistant.FinancialScore.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.FinancialScore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddFinancialScoreApplication(this IServiceCollection services)
    {
        services.AddSingleton<FinancialScoreCalculator>();
        services.AddSingleton<FinancialScoreService>();
        return services;
    }
}
