using FinancialAssistant.Identity.Infrastructure.Storage;

namespace FinancialAssistant.Identity.Tests;

public sealed class IdentityRetentionPolicyTests
{
    [Fact]
    public void Policies_CoverEveryOwnedIdentityEntity()
    {
        var indexEntities = IdentityIndexCatalog.Create("test")
            .Select(definition => definition.Entity)
            .OrderBy(entity => entity)
            .ToArray();
        var policyEntities = IdentityRetentionPolicies.All
            .Select(policy => policy.Entity)
            .OrderBy(entity => entity)
            .ToArray();

        Assert.Equal(indexEntities, policyEntities);
    }

    [Fact]
    public void SessionPolicy_PreservesReplayEvidenceButHasHardMaximumAge()
    {
        var policy = Assert.Single(
            IdentityRetentionPolicies.All,
            item => item.Entity == IdentityIndexCatalog.SessionsEntity);

        Assert.Equal(TimeSpan.FromDays(30), policy.RetainAfterTerminalState);
        Assert.Equal(TimeSpan.FromDays(90), policy.HardMaximumDocumentAge);
        Assert.Contains("replay", policy.Notes, StringComparison.OrdinalIgnoreCase);
    }
}
