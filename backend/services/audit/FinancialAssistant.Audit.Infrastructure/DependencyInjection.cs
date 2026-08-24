using FinancialAssistant.Audit.Application;
using FinancialAssistant.Audit.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.Audit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuditInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(AuditOptions.SectionName);
        services.Configure<AuditOptions>(section);
        var options = section.Get<AuditOptions>() ?? new AuditOptions();
        Validate(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new AuditPolicy(options.AllowedProducers, options.RetentionDays));
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
