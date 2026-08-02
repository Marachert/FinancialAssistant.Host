using FinancialAssistant.Expense.Application;
using FinancialAssistant.TransactionIntake.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Expense.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddExpenseInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryExpenseRecordStore>();
        services.AddSingleton<IExpenseRecordStore>(provider =>
            provider.GetRequiredService<InMemoryExpenseRecordStore>());
        services.AddSingleton<InMemoryExpenseRecordEventPublisher>();
        services.AddSingleton<IExpenseRecordEventPublisher>(provider =>
            provider.GetRequiredService<InMemoryExpenseRecordEventPublisher>());
        services.AddSingleton<ITransactionConfirmedConsumer, ExpenseTransactionConfirmedConsumer>();
        return services;
    }
}
