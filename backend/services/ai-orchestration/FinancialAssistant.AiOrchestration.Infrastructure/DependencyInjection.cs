using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Infrastructure.Prompts;
using FinancialAssistant.AiOrchestration.Infrastructure.Providers;
using FinancialAssistant.AiOrchestration.Infrastructure.Routing;
using FinancialAssistant.AiOrchestration.Infrastructure.Storage;
using FinancialAssistant.AiOrchestration.Infrastructure.Usage;
using FinancialAssistant.AiOrchestration.Infrastructure.Validation;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FinancialAssistant.AiOrchestration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAiOrchestrationInfrastructure(this IServiceCollection services, IConfiguration? configuration = null, bool windowsNative = false)
    {
        services.AddSingleton<IModelRouter, StaticModelRouter>();
        services.AddSingleton(TransactionParsingPromptCatalog.Version1);
        services.AddSingleton(TransactionParsingPromptCatalog.ExecutionPolicy);
        services.AddSingleton<IPromptRegistry, InMemoryPromptRegistry>();
        services.AddSingleton<ILlmProviderResolver, RegisteredLlmProviderResolver>();
        services.AddSingleton<IStructuredOutputValidator, JsonSchemaStructuredOutputValidator>();
        if (windowsNative)
        {
            if (configuration?.GetValue<bool>("AiOrchestration:Provider:Enabled") == true)
            { throw new InvalidOperationException("Live AI must remain disabled in the native POC development profile."); }
            var settings = configuration?.GetSection("AiOrchestration:Storage:Postgres").Get<NativePostgresSettings>()
                ?? throw new InvalidOperationException("Native AI persistence configuration is missing.");
            var options = settings.CreateOptions("ai-orchestration");
            using (var validation = settings.CreateProtection()) { }
            services.AddSingleton(_ => settings.CreateProtection());
            services.AddSingleton(provider => new OwnedPostgresStore(options, provider.GetRequiredService<RecordProtection>()));
            services.AddSingleton<PostgresAiCallMetadataStore>();
            services.AddSingleton<IAiCallMetadataStore>(provider => provider.GetRequiredService<PostgresAiCallMetadataStore>());
            services.AddSingleton<IAiUsageLimiter, PostgresAiUsageLimiter>();
            services.AddSingleton<PostgresAiExecutionJournal>();
            services.RemoveAll<IAiOrchestrationService>();
            services.AddSingleton<AiOrchestrationService>();
            services.AddSingleton<IAiOrchestrationService, DurableAiOrchestrationService>();
            services.AddSingleton<OwnedStorageHealthCheck>();
            services.AddHealthChecks().AddCheck<OwnedStorageHealthCheck>("ai-owned-storage", tags: new[] { "ready" });
        }
        else
        {
            services.AddSingleton<InMemoryAiCallMetadataStore>();
            services.AddSingleton<IAiCallMetadataStore>(provider => provider.GetRequiredService<InMemoryAiCallMetadataStore>());
            services.AddSingleton<IAiUsageLimiter, InMemoryAiUsageLimiter>();
        }
        return services;
    }
}
