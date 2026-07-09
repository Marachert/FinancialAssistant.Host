using FinancialAssistant.Profile.Contracts;
using FinancialAssistant.Profile.Domain;
using FinancialAssistant.Profile.Infrastructure.Storage;

namespace FinancialAssistant.Profile.Tests;

public sealed class ProfileArchitectureTests
{
    [Fact]
    public void Domain_DoesNotReferenceInfrastructureOrProviderClients()
    {
        var references = typeof(ProfileDomainAssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        Assert.DoesNotContain("FinancialAssistant.Profile.Infrastructure", references);
        Assert.DoesNotContain("Elastic.Clients.Elasticsearch", references);
        Assert.DoesNotContain("RabbitMQ.Client", references);
    }

    [Fact]
    public void RegistrationEvent_DoesNotCopySensitiveIdentityAttributesIntoProfile()
    {
        var prohibitedFragments = new[]
        {
            "Email",
            "Phone",
            "DisplayName",
            "FirstName",
            "LastName",
            "ProviderToken",
            "RawSubject"
        };

        var propertyNames = typeof(UserRegisteredProfileEvent)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        foreach (var prohibited in prohibitedFragments)
        {
            Assert.DoesNotContain(
                propertyNames,
                propertyName => propertyName.Contains(prohibited, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void RuntimeStore_IsDevelopmentOnlyInMemoryAdapter()
    {
        Assert.Contains(
            "InMemory",
            typeof(InMemoryProfileStore).Name,
            StringComparison.Ordinal);
    }
}
