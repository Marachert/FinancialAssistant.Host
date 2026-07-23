using FinancialAssistant.AiOrchestration.Application;
using FinancialAssistant.AiOrchestration.Domain;
using FinancialAssistant.AiOrchestration.Infrastructure.Prompts;
using FinancialAssistant.AiOrchestration.Infrastructure.Routing;

namespace FinancialAssistant.AiOrchestration.Tests;

public sealed class RegistryAndRoutingTests
{
    [Fact]
    public void PromptRegistry_ReturnsRequestedVersionOrLatest()
    {
        var registry = new InMemoryPromptRegistry(
            new[]
            {
                new PromptDefinition("transaction.parse", 1, "v1", "{}"),
                new PromptDefinition("transaction.parse", 2, "v2", "{}"),
            });

        Assert.Equal(1, registry.GetRequired("transaction.parse", 1).Version);
        Assert.Equal(2, registry.GetRequired("TRANSACTION.PARSE").Version);
    }

    [Fact]
    public void PromptRegistry_WhenPromptMissing_Throws()
    {
        var registry = new InMemoryPromptRegistry(Array.Empty<PromptDefinition>());

        Assert.Throws<PromptNotFoundException>(() => registry.GetRequired("missing"));
    }

    [Fact]
    public void ModelRouter_ReturnsNamedCapabilityRoute()
    {
        var router = new StaticModelRouter(
            new[] { new AiModelRoute("transaction.parse", "synthetic-provider", "model-a") });

        var route = router.GetRequiredRoute("TRANSACTION.PARSE");

        Assert.Equal("synthetic-provider", route.Provider);
        Assert.Equal("model-a", route.Model);
    }

    [Fact]
    public void ModelRouter_WhenCapabilityMissing_Throws()
    {
        var router = new StaticModelRouter(Array.Empty<AiModelRoute>());

        Assert.Throws<AiCapabilityNotConfiguredException>(() =>
            router.GetRequiredRoute("missing"));
    }
}
