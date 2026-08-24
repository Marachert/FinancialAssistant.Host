using FinancialAssistant.Mcp.Application;
using FinancialAssistant.Mcp.Infrastructure;
using Xunit;

namespace FinancialAssistant.Mcp.Tests;

public sealed class McpArchitectureTests
{
    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrProviderSdks()
    {
        var references = typeof(McpToolRegistry).Assembly
            .GetReferencedAssemblies()
            .Select(assembly => assembly.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(typeof(DependencyInjection).Assembly.GetName().Name, references);
        Assert.DoesNotContain("ModelContextProtocol", references);
        Assert.DoesNotContain("Nest", references);
        Assert.DoesNotContain("Elastic.Clients.Elasticsearch", references);
    }
}
