using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Expense.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddExpenseApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IExpenseManagementService, ExpenseManagementService>();
        return services;
    }
}
