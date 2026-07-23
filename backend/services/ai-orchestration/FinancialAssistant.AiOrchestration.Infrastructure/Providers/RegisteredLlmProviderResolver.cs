using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Application.Abstractions;

namespace FinancialAssistant.AiOrchestration.Infrastructure.Providers;

public sealed class RegisteredLlmProviderResolver : ILlmProviderResolver
{
    private readonly IReadOnlyDictionary<string, ILlmProvider> providers;

    public RegisteredLlmProviderResolver(IEnumerable<ILlmProvider> providers)
    {
        this.providers = providers.ToDictionary(
            provider => provider.Name,
            StringComparer.OrdinalIgnoreCase);
    }

    public ILlmProvider GetRequired(string providerName) =>
        providers.TryGetValue(providerName, out var provider)
            ? provider
            : throw new LlmProviderNotConfiguredException(providerName);
}
