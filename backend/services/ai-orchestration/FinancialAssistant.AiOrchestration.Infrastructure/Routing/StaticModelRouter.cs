using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Domain;

namespace FinancialAssistant.AiOrchestration.Infrastructure.Routing;

public sealed class StaticModelRouter : IModelRouter
{
    private readonly IReadOnlyDictionary<string, AiModelRoute> routes;

    public StaticModelRouter(IEnumerable<AiModelRoute> routes)
    {
        this.routes = routes.ToDictionary(
            route => route.CapabilityName,
            StringComparer.OrdinalIgnoreCase);
    }

    public AiModelRoute GetRequiredRoute(string capabilityName) =>
        routes.TryGetValue(capabilityName, out var route)
            ? route
            : throw new AiCapabilityNotConfiguredException(capabilityName);
}
