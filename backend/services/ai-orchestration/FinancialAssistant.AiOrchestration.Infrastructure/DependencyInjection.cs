using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Infrastructure.Prompts;
using FinancialAssistant.AiOrchestration.Infrastructure.Providers;
using FinancialAssistant.AiOrchestration.Infrastructure.Routing;
using FinancialAssistant.AiOrchestration.Infrastructure.Storage;
using FinancialAssistant.AiOrchestration.Infrastructure.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialAssistant.AiOrchestration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAiOrchestrationInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IModelRouter, StaticModelRouter>();
        services.AddSingleton<IPromptRegistry, InMemoryPromptRegistry>();
        services.AddSingleton<ILlmProviderResolver, RegisteredLlmProviderResolver>();
        services.AddSingleton<IStructuredOutputValidator, JsonSchemaStructuredOutputValidator>();
        services.AddSingleton<InMemoryAiCallMetadataStore>();
        services.AddSingleton<IAiCallMetadataStore>(provider =>
            provider.GetRequiredService<InMemoryAiCallMetadataStore>());
        return services;
    }
}
