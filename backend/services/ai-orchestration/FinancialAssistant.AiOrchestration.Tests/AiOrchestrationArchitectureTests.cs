using FinancialAssistant.AiOrchestration.Api.Configuration;
using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Contracts;
using FinancialAssistant.AiOrchestration.Domain;
using FinancialAssistant.AiOrchestration.Infrastructure.Storage;

namespace FinancialAssistant.AiOrchestration.Tests;

public sealed class AiOrchestrationArchitectureTests
{
    [Fact]
    public void Domain_DoesNotReferenceInfrastructureOrProviderClients()
    {
        var references = typeof(AiOrchestrationDomainAssemblyMarker)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        Assert.DoesNotContain("FinancialAssistant.AiOrchestration.Infrastructure", references);
        Assert.DoesNotContain(references, name =>
            name!.Contains("OpenAI", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Anthropic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProviderBoundary_IsOwnedByApplication()
    {
        Assert.Equal(
            "FinancialAssistant.AiOrchestration.Application",
            typeof(ILlmProvider).Assembly.GetName().Name);
    }

    [Fact]
    public void PublicContracts_ExplicitlyDescribeSuggestionReview()
    {
        Assert.Equal(
            "FinancialAssistant.AiOrchestration.Contracts",
            typeof(AiCapabilityRequest).Assembly.GetName().Name);

        var review = new AiSuggestionReview(
            Confidence: null,
            Ambiguities: new[] { "unverified_ai_output" },
            RequiresReview: true);
        Assert.True(review.RequiresReview);
        Assert.DoesNotContain(
            typeof(AiCapabilityResult).GetProperties(),
            property =>
                property.Name.Contains("Confirmed", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Contains("Persisted", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ApiBoundary_DoesNotReferenceAuthoritativeFinancialServices()
    {
        var references = typeof(Program)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .ToArray();

        Assert.DoesNotContain(references, name =>
            name!.Contains("Expense", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Income", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("TransactionIntake", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProviderPlaceholders_CannotHoldCredentials()
    {
        var properties = typeof(AiProviderPlaceholderOptions)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(properties, name =>
            name.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RuntimeMetadataAdapter_IsExplicitlyInMemoryForCurrentIncrement()
    {
        Assert.Contains("InMemory", typeof(InMemoryAiCallMetadataStore).Name, StringComparison.Ordinal);
    }

    [Fact]
    public void MetadataContract_CannotStoreRawPromptsInputsOutputsOrErrors()
    {
        var properties = typeof(AiCallMetadata).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain(properties, name =>
            name.Contains("Input", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("PromptTemplate", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Output", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Error", StringComparison.OrdinalIgnoreCase));
    }
}
