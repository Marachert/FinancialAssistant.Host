using FinancialAssistant.Audit.Application;
using FinancialAssistant.Audit.Contracts;
using FinancialAssistant.Audit.Infrastructure.Persistence;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Audit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuditInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool windowsNative = false)
    {
        var section = configuration.GetSection(AuditOptions.SectionName);
        services.Configure<AuditOptions>(section);
        var options = section.Get<AuditOptions>() ?? new AuditOptions();
        Validate(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new AuditPolicy(options.AllowedProducers, options.RetentionDays));
        if (windowsNative)
        {
            var settings = configuration.GetSection("Audit:Storage:Postgres").Get<NativePostgresSettings>()
                ?? throw new InvalidOperationException("Native Audit persistence configuration is missing.");
            var storage = settings.CreateOptions("audit");
            using (var validation = settings.CreateProtection()) { }
            services.AddSingleton(_ => settings.CreateProtection());
            services.AddSingleton(provider => new OwnedPostgresStore(storage, provider.GetRequiredService<RecordProtection>()));
            services.AddSingleton(provider => new PostgresAuditRecordStore(provider.GetRequiredService<OwnedPostgresStore>(),
                provider.GetRequiredService<AuditPolicy>(), options.RetentionDays, provider.GetRequiredService<TimeProvider>()));
            services.AddSingleton<IAuditRecordStore>(provider => provider.GetRequiredService<PostgresAuditRecordStore>());
            services.AddSingleton<IAuditEventTransaction>(provider => provider.GetRequiredService<PostgresAuditRecordStore>());
            services.AddSingleton<AuditEventService>();
            services.AddSingleton<IAuditEventConsumer>(provider => provider.GetRequiredService<AuditEventService>());
            services.AddSingleton<AuditEventMessageHandler>();
            services.AddHostedService<AuditRetentionWorker>();
            services.AddSingleton<OwnedStorageHealthCheck>();
            services.AddHealthChecks().AddCheck<OwnedStorageHealthCheck>("audit-owned-storage", tags: new[] { "ready" });
            return services;
        }
        services.AddSingleton<IAuditRecordStore, InMemoryAppendOnlyAuditRecordStore>();
        services.AddSingleton<AuditEventService>();
        services.AddSingleton<IAuditEventConsumer>(provider =>
            provider.GetRequiredService<AuditEventService>());
        services.AddSingleton<AuditEventMessageHandler>();
        services.AddHostedService<RabbitMqAuditEventConsumer>();
        return services;
    }

    private static void Validate(AuditOptions options)
    {
        if (options.AllowedProducers.Length == 0
            || options.RetentionDays.Count == 0
            || options.RetentionDays.Any(item => item.Value is < 1 or > 3650))
        {
            throw new InvalidOperationException(
                "Audit producers and 1-3650 day retention classes are required.");
        }
    }
}
