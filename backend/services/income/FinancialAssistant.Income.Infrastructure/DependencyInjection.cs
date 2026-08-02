using FinancialAssistant.Income.Application;
using FinancialAssistant.TransactionIntake.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Income.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIncomeInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryIncomeRecordStore>();
        services.AddSingleton<IIncomeRecordStore>(provider =>
            provider.GetRequiredService<InMemoryIncomeRecordStore>());
        services.AddSingleton<InMemoryIncomeRecordEventPublisher>();
        services.AddSingleton<IIncomeRecordEventPublisher>(provider =>
            provider.GetRequiredService<InMemoryIncomeRecordEventPublisher>());
        services.AddSingleton<ITransactionConfirmedConsumer, IncomeTransactionConfirmedConsumer>();
        return services;
    }
}
