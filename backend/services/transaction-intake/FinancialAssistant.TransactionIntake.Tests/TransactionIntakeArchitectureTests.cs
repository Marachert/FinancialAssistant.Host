using System.Reflection;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Domain;
using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Tests;

public sealed class TransactionIntakeArchitectureTests
{
    [Fact]
    public void Domain_DoesNotReferenceApiOrInfrastructure()
    {
        var references = typeof(TransactionIntakeDomainAssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        Assert.DoesNotContain("FinancialAssistant.TransactionIntake.Api", references);
        Assert.DoesNotContain("FinancialAssistant.TransactionIntake.Infrastructure", references);
    }

    [Fact]
    public void DraftContract_DoesNotExposeRawInputOrIdempotencyMaterial()
    {
        var properties = typeof(TransactionDraft)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(properties, name => name.Contains("Raw", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("InputText", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("IdempotencyKey", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ConfirmedEvent_DoesNotExposeParserOrIdempotencyMaterial()
    {
        var properties = typeof(TransactionConfirmedIntegrationEvent)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(properties, name => name.Contains("Input", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("Idempotency", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("Confidence", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(properties, name => name.Contains("Ambiguit", StringComparison.OrdinalIgnoreCase));
    }
}
