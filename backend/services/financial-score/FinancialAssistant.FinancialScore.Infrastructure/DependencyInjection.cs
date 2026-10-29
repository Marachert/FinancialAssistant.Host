using FinancialAssistant.FinancialScore.Application;
using FinancialAssistant.FinancialScore.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.FinancialScore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFinancialScoreInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool windowsNative = false)
    {
        services.Configure<FinancialScoreServiceOptions>(
            configuration.GetSection(FinancialScoreServiceOptions.SectionName));
        var options = configuration
            .GetSection(FinancialScoreServiceOptions.SectionName)
            .Get<FinancialScoreServiceOptions>() ?? new FinancialScoreServiceOptions();

        if (windowsNative)
        {
            var settings = configuration.GetSection("FinancialScore:Storage:Postgres").Get<NativePostgresSettings>()
                ?? throw new InvalidOperationException("Native Score persistence configuration is missing.");
            var storage = settings.CreateOptions("financial-score");
            using (var validation = settings.CreateProtection()) { }
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton(_ => settings.CreateProtection());
            services.AddSingleton(provider => new OwnedPostgresStore(storage, provider.GetRequiredService<RecordProtection>()));
            services.AddSingleton<ScorePersistenceContext>();
            services.AddSingleton<IFinancialScoreTransaction>(provider => provider.GetRequiredService<ScorePersistenceContext>());
            services.AddSingleton<PostgresFinancialScoreStore>();
            services.AddSingleton<IFinancialScoreStore>(provider => provider.GetRequiredService<PostgresFinancialScoreStore>());
            services.AddSingleton<PostgresFinancialScoreEventPublisher>();
            services.AddSingleton<IFinancialScoreEventPublisher>(provider => provider.GetRequiredService<PostgresFinancialScoreEventPublisher>());
            services.AddSingleton<PostgresFinancialScoreProfileSettingsProvider>();
            services.AddSingleton<IFinancialScoreProfileSettingsProvider>(provider => provider.GetRequiredService<PostgresFinancialScoreProfileSettingsProvider>());
            services.AddSingleton<FinancialScoreFinancialEventMessageHandler>();
            services.AddSingleton<OwnedStorageHealthCheck>();
            services.AddHealthChecks().AddCheck<OwnedStorageHealthCheck>("score-owned-storage", tags: new[] { "ready" });
            return services;
        }

        services.AddSingleton<InMemoryFinancialScoreStore>();
        services.AddSingleton<IFinancialScoreStore>(provider =>
            provider.GetRequiredService<InMemoryFinancialScoreStore>());
        if (string.Equals(options.Events.Mode, "RabbitMq", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IFinancialScoreEventPublisher, RabbitMqFinancialScoreEventPublisher>();
        }
        else
        {
            services.AddSingleton<InMemoryFinancialScoreEventPublisher>();
            services.AddSingleton<IFinancialScoreEventPublisher>(provider =>
                provider.GetRequiredService<InMemoryFinancialScoreEventPublisher>());
        }

        services.AddSingleton<InMemoryFinancialScoreProfileSettingsProvider>();
        services.AddSingleton<IFinancialScoreProfileSettingsProvider>(provider =>
            provider.GetRequiredService<InMemoryFinancialScoreProfileSettingsProvider>());
        services.AddSingleton<FinancialScoreFinancialEventMessageHandler>();
        services.AddHostedService<FinancialScoreFinancialEventConsumer>();
        return services;
    }
}
