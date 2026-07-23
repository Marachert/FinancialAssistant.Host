using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Application.Drafts;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.TransactionIntake.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddTransactionIntakeApplication(this IServiceCollection services)
    {
        services.AddSingleton<ITransactionIntakeClock, SystemTransactionIntakeClock>();
        services.AddSingleton<ITransactionDraftIdGenerator, GuidTransactionDraftIdGenerator>();
        services.AddSingleton<ITransactionConfirmationIdGenerator, GuidTransactionConfirmationIdGenerator>();
        services.AddSingleton<TransactionDraftValidator>();
        services.AddSingleton<ITransactionIntakeService, TransactionIntakeService>();
        services.AddSingleton<ITransactionConfirmationService, TransactionConfirmationService>();
        services.AddSingleton<IOcrCompletedConsumer, OcrCompletedDraftConsumer>();
        return services;
    }
}
