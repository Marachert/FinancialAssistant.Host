using FinancialAssistant.Category.Contracts;
using FinancialAssistant.Category.Domain;
using FinancialAssistant.Category.Domain.Categories;
using FinancialAssistant.Category.Infrastructure.Events;
using FinancialAssistant.Category.Infrastructure.Storage;

namespace FinancialAssistant.Category.Tests;

public sealed class CategoryArchitectureTests
{
    [Fact]
    public void Domain_DoesNotReferenceInfrastructureOrProviderClients()
    {
        var references = typeof(CategoryDomainAssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        Assert.DoesNotContain("FinancialAssistant.Category.Infrastructure", references);
        Assert.DoesNotContain("Elastic.Clients.Elasticsearch", references);
        Assert.DoesNotContain("RabbitMQ.Client", references);
    }

    [Fact]
    public void DefaultTaxonomy_HasStableUniqueIdentifiersAndOrdering()
    {
        var categories = DefaultCategoryTaxonomy.Create(DateTimeOffset.UnixEpoch);

        Assert.Equal(11, categories.Count);
        Assert.Equal(categories.Count, categories.Select(category => category.Id).Distinct().Count());
        Assert.Equal(
            categories.OrderBy(category => category.SortOrder).Select(category => category.Id),
            categories.Select(category => category.Id));
        Assert.All(categories, category => Assert.Equal(category.Id, category.Key));
    }

    [Fact]
    public void CategoryUpdatedEvent_DoesNotExposeAliasesOrMerchantText()
    {
        Assert.Equal("category.updated.v1", CategoryUpdatedIntegrationEvent.Name);

        var propertyNames = typeof(CategoryUpdatedIntegrationEvent)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(propertyNames, name => name.Contains("Alias", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Merchant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RuntimeAdapters_AreExplicitlyInMemoryForCurrentIncrement()
    {
        Assert.Contains("InMemory", typeof(InMemoryCategoryStore).Name, StringComparison.Ordinal);
        Assert.Contains("InMemory", typeof(InMemoryCategoryEventPublisher).Name, StringComparison.Ordinal);
    }
}
