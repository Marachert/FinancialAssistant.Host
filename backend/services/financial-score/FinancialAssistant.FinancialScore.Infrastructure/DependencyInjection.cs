using FinancialAssistant.FinancialScore.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.FinancialScore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddFinancialScoreInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FinancialScoreServiceOptions>(
            configuration.GetSection(FinancialScoreServiceOptions.SectionName));
        var options = configuration
            .GetSection(FinancialScoreServiceOptions.SectionName)
            .Get<FinancialScoreServiceOptions>() ?? new FinancialScoreServiceOptions();

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
