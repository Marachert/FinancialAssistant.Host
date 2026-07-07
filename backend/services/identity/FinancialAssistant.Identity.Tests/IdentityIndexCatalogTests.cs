using FinancialAssistant.Identity.Infrastructure.Storage;

namespace FinancialAssistant.Identity.Tests;

public sealed class IdentityIndexCatalogTests
{
    [Fact]
    public void Create_ReturnsFourServiceOwnedIndicesWithStableAliases()
    {
        var definitions = IdentityIndexCatalog.Create("dev");

        Assert.Equal(4, definitions.Count);
        Assert.Contains(definitions, item => item.PhysicalIndex == "fa-dev-identity-accounts-v1-000001");
        Assert.Contains(definitions, item => item.ReadAlias == "fa-dev-identity-credentials-read");
        Assert.Contains(definitions, item => item.WriteAlias == "fa-dev-identity-sessions-write");
        Assert.Contains(definitions, item => item.PhysicalIndex == "fa-dev-identity-external-identities-v1-000001");
        Assert.Equal(definitions.Count, definitions.Select(item => item.ReadAlias).Distinct().Count());
        Assert.Equal(definitions.Count, definitions.Select(item => item.WriteAlias).Distinct().Count());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Dev Environment")]
    [InlineData("dev_1")]
    public void Create_RejectsUnsafeEnvironmentSegments(string environment)
    {
        Assert.ThrowsAny<ArgumentException>(() => IdentityIndexCatalog.Create(environment));
    }
}
