using FinancialAssistant.ReceiptProcessing.Contracts;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Application.Abstractions;
using FinancialAssistant.TransactionIntake.Application.Drafts;
using FinancialAssistant.TransactionIntake.Infrastructure.Events;
using FinancialAssistant.TransactionIntake.Infrastructure.Parsing;
using FinancialAssistant.TransactionIntake.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialAssistant.TransactionIntake.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTransactionIntakeInfrastructure(this IServiceCollection services, IConfiguration? configuration = null, bool windowsNative = false)
    {
        services.AddSingleton<ITransactionInputParser, DeterministicTransactionInputParser>();
        if (windowsNative)
        {
            var settings = configuration?.GetSection("TransactionIntake:Storage:Postgres").Get<NativePostgresSettings>()
                ?? throw new InvalidOperationException("Native Intake persistence configuration is missing.");
            var retention = configuration?.GetSection("TransactionIntake:Storage").Get<IntakeRetentionSettings>()
                ?? throw new InvalidOperationException("Native Intake source retention configuration is missing.");
            retention.Validate();
            var options = settings.CreateOptions("transaction-intake");
            using (var validation = settings.CreateProtection()) { }
            services.AddSingleton(retention);
            services.AddSingleton(_ => settings.CreateProtection());
            services.AddSingleton(provider => new OwnedPostgresStore(options, provider.GetRequiredService<RecordProtection>()));
            services.AddSingleton<IntakePersistenceContext>();
            services.AddSingleton<ITransactionIntakeMutationScope>(provider => provider.GetRequiredService<IntakePersistenceContext>());
            services.AddSingleton<PostgresTransactionDraftStore>();
            services.AddSingleton<ITransactionDraftStore>(provider => provider.GetRequiredService<PostgresTransactionDraftStore>());
            services.AddSingleton<PostgresTransactionDraftCreationStore>();
            services.AddSingleton<ITransactionDraftCreationStore>(provider => provider.GetRequiredService<PostgresTransactionDraftCreationStore>());
            services.AddSingleton<PostgresTransactionConfirmationStore>();
            services.AddSingleton<ITransactionConfirmationStore>(provider => provider.GetRequiredService<PostgresTransactionConfirmationStore>());
            services.AddSingleton<PostgresIntakeOutbox>();
            services.AddSingleton<ITransactionDraftCreatedPublisher>(provider => provider.GetRequiredService<PostgresIntakeOutbox>());
            services.AddSingleton<ITransactionConfirmedPublisher>(provider => provider.GetRequiredService<PostgresIntakeOutbox>());
            services.RemoveAll<ITransactionDraftReviewService>();
            services.AddSingleton<TransactionDraftReviewService>();
            services.AddSingleton<ITransactionDraftReviewService, TransactionalDraftReviewService>();
            services.RemoveAll<ITransactionConfirmationService>();
            services.AddSingleton<TransactionConfirmationService>();
            services.AddSingleton<ITransactionConfirmationService, TransactionalConfirmationService>();
            services.RemoveAll<IOcrCompletedConsumer>();
            services.AddSingleton<OcrCompletedDraftConsumer>();
            services.AddSingleton<IOcrCompletedConsumer, TransactionalOcrDraftConsumer>();
            services.AddHostedService<IntakeRetentionWorker>();
            services.AddSingleton<OwnedStorageHealthCheck>();
            services.AddHealthChecks().AddCheck<OwnedStorageHealthCheck>("intake-owned-storage", tags: new[] { "ready" });
            return services;
        }
        services.AddSingleton<ITransactionDraftStore, InMemoryTransactionDraftStore>();
        services.AddSingleton<ITransactionDraftCreationStore, InMemoryTransactionDraftCreationStore>();
        services.AddSingleton<ITransactionConfirmationStore, InMemoryTransactionConfirmationStore>();
        services.AddSingleton<InMemoryTransactionDraftCreatedPublisher>();
        services.AddSingleton<ITransactionDraftCreatedPublisher>(provider =>
            provider.GetRequiredService<InMemoryTransactionDraftCreatedPublisher>());
        services.AddSingleton<InMemoryTransactionConfirmedPublisher>();
        services.AddSingleton<ITransactionConfirmedPublisher>(provider =>
            provider.GetRequiredService<InMemoryTransactionConfirmedPublisher>());
        return services;
    }
}
