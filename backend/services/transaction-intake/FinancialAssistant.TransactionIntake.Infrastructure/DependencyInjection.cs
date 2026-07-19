using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Infrastructure.Events;
using FinancialAssistant.TransactionIntake.Infrastructure.Parsing;
using FinancialAssistant.TransactionIntake.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.TransactionIntake.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTransactionIntakeInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ITransactionInputParser, DeterministicTransactionInputParser>();
        services.AddSingleton<ITransactionDraftStore, InMemoryTransactionDraftStore>();
        services.AddSingleton<ITransactionConfirmationStore, InMemoryTransactionConfirmationStore>();
        services.AddSingleton<InMemoryTransactionConfirmedPublisher>();
        services.AddSingleton<ITransactionConfirmedPublisher>(provider =>
            provider.GetRequiredService<InMemoryTransactionConfirmedPublisher>());
        return services;
    }
}
