namespace FinancialAssistant.PublicApiGateway.Tests;

public sealed class GatewayArchitectureTests
{
    [Fact]
    public void GatewayAssembly_DoesNotReferenceSearchStorageClients()
    {
        var disallowedReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Elastic.Clients.Elasticsearch",
            "Elasticsearch.Net",
            "Nest"
        };

        var matchingReferences = typeof(Program).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && disallowedReferences.Contains(name))
            .ToArray();

        Assert.Empty(matchingReferences);
    }
}
