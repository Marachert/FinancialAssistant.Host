using FinancialAssistant.Identity.Domain;

namespace FinancialAssistant.Identity.Tests;

public sealed class IdentityArchitectureTests
{
    [Fact]
    public void Domain_DoesNotReferenceInfrastructureOrElasticsearchClients()
    {
        var references = typeof(IdentityDomainAssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        Assert.DoesNotContain("FinancialAssistant.Identity.Infrastructure", references);
        Assert.DoesNotContain("Elastic.Clients.Elasticsearch", references);
        Assert.DoesNotContain("Elasticsearch.Net", references);
        Assert.DoesNotContain("Nest", references);
    }
}
