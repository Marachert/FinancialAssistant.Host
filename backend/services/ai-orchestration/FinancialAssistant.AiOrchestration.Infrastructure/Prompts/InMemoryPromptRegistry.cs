using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Domain;

namespace FinancialAssistant.AiOrchestration.Infrastructure.Prompts;

public sealed class InMemoryPromptRegistry : IPromptRegistry
{
    private readonly IReadOnlyDictionary<(string Name, int Version), PromptDefinition> prompts;

    public InMemoryPromptRegistry(IEnumerable<PromptDefinition> prompts)
    {
        this.prompts = prompts.ToDictionary(
            prompt => (prompt.Name.ToUpperInvariant(), prompt.Version));
    }

    public PromptDefinition GetRequired(string promptName, int? version = null)
    {
        if (version is not null &&
            prompts.TryGetValue((promptName.ToUpperInvariant(), version.Value), out var requested))
        {
            return requested;
        }

        if (version is null)
        {
            var latest = prompts.Values
                .Where(prompt => string.Equals(prompt.Name, promptName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(prompt => prompt.Version)
                .FirstOrDefault();
            if (latest is not null)
            {
                return latest;
            }
        }

        throw new PromptNotFoundException(promptName, version);
    }
}
