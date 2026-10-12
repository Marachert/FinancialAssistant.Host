using FinancialAssistant.Income.Application;
using FinancialAssistant.Income.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialAssistant.Income.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIncomeInfrastructure(this IServiceCollection services, IConfiguration? configuration = null, bool windowsNative = false)
    {
        if (windowsNative)
        {
            var settings = configuration?.GetSection("Income:Storage:Postgres").Get<NativePostgresSettings>()
                ?? throw new InvalidOperationException("Native Income persistence configuration is missing.");
            var options = settings.CreateOptions("income");
            using (var validation = settings.CreateProtection()) { }
            services.AddSingleton(_ => settings.CreateProtection());
            services.AddSingleton(provider => new OwnedPostgresStore(options, provider.GetRequiredService<RecordProtection>()));
            services.AddSingleton<IncomePersistenceContext>();
            services.AddSingleton<PostgresIncomeRecordStore>();
            services.AddSingleton<IIncomeRecordStore>(provider => provider.GetRequiredService<PostgresIncomeRecordStore>());
            services.AddSingleton<PostgresIncomeRecordEventPublisher>();
            services.AddSingleton<IIncomeRecordEventPublisher>(provider => provider.GetRequiredService<PostgresIncomeRecordEventPublisher>());
            services.RemoveAll<IIncomeManagementService>();
            services.AddSingleton<IncomeManagementService>();
            services.AddSingleton<IIncomeManagementService, TransactionalIncomeManagementService>();
            services.AddSingleton<IncomeTransactionConfirmedConsumer>();
            services.AddSingleton<ITransactionConfirmedConsumer, TransactionalIncomeConfirmedConsumer>();
            services.AddHostedService<IncomeOutboxRetentionWorker>();
            services.AddSingleton<OwnedStorageHealthCheck>();
            services.AddHealthChecks().AddCheck<OwnedStorageHealthCheck>("income-owned-storage", tags: new[] { "ready" });
            return services;
        }
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
