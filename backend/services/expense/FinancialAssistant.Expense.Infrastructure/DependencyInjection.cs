using FinancialAssistant.Expense.Application;
using FinancialAssistant.Expense.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialAssistant.Expense.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddExpenseInfrastructure(this IServiceCollection services, IConfiguration? configuration = null, bool windowsNative = false)
    {
        if (windowsNative)
        {
            var settings = configuration?.GetSection("Expense:Storage:Postgres").Get<NativePostgresSettings>()
                ?? throw new InvalidOperationException("Native Expense persistence configuration is missing.");
            var options = settings.CreateOptions("expense");
            using (var validation = settings.CreateProtection()) { }
            services.AddSingleton(_ => settings.CreateProtection());
            services.AddSingleton(provider => new OwnedPostgresStore(options, provider.GetRequiredService<RecordProtection>()));
            services.AddSingleton<ExpensePersistenceContext>();
            services.AddSingleton<PostgresExpenseRecordStore>();
            services.AddSingleton<IExpenseRecordStore>(provider => provider.GetRequiredService<PostgresExpenseRecordStore>());
            services.AddSingleton<PostgresExpenseRecordEventPublisher>();
            services.AddSingleton<IExpenseRecordEventPublisher>(provider => provider.GetRequiredService<PostgresExpenseRecordEventPublisher>());
            services.RemoveAll<IExpenseManagementService>();
            services.AddSingleton<ExpenseManagementService>();
            services.AddSingleton<IExpenseManagementService, TransactionalExpenseManagementService>();
            services.AddSingleton<ExpenseTransactionConfirmedConsumer>();
            services.AddSingleton<ITransactionConfirmedConsumer, TransactionalExpenseConfirmedConsumer>();
            services.AddHostedService<ExpenseOutboxRetentionWorker>();
            NativeExpenseMessaging.Add(services, configuration);
            services.AddSingleton<OwnedStorageHealthCheck>();
            services.AddHealthChecks().AddCheck<OwnedStorageHealthCheck>("expense-owned-storage", tags: new[] { "ready" });
            return services;
        }
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
