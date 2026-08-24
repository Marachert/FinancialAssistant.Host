using System.Reflection;
using FinancialAssistant.Audit.Application;
using FinancialAssistant.Audit.Contracts;
using Xunit;

namespace FinancialAssistant.Audit.Tests;

public sealed class AuditArchitectureTests
{
    [Fact]
    public void ContractsAndStoreExposeNoUpdateOrDeleteMutation()
    {
        var mutationNames = typeof(IAuditRecordStore)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => method.Name)
            .Where(name => name.Contains("Update", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(mutationNames);
        Assert.True(typeof(AuditEventV1).IsSealed);
        Assert.True(typeof(AuditRecordResponse).IsSealed);
    }
}
