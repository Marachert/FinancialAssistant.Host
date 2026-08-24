namespace FinancialAssistant.Monitoring.Tests;

using Xunit;

public sealed class MonitoringArchitectureTests
{
    [Fact]
    public void Contracts_DoNotReferenceImplementationOrStorageAssemblies()
    {
        var references = typeof(Contracts.MonitoringDashboardResponse)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        Assert.DoesNotContain("FinancialAssistant.Monitoring.Application", references);
        Assert.DoesNotContain("FinancialAssistant.Monitoring.Infrastructure", references);
        Assert.DoesNotContain("Elastic.Clients.Elasticsearch", references);
        Assert.DoesNotContain("RabbitMQ.Client", references);
    }
}
