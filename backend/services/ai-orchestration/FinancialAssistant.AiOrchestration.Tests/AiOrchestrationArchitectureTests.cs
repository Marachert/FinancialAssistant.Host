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
    public void ProviderConfiguration_CannotHoldCredentials()
    {
        var properties = typeof(AiProviderClientOptions)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(properties, name =>
            name.Contains("Key", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Token", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(nameof(AiProviderClientOptions.RequestTimeoutSeconds), properties);
        Assert.Contains(nameof(AiProviderClientOptions.MaximumAttempts), properties);
        Assert.Contains(nameof(AiProviderClientOptions.RetryDelayMilliseconds), properties);
    }

    [Fact]
    public void RuntimeMetadataAdapter_IsExplicitlyInMemoryForCurrentIncrement()
    {
        Assert.Contains("InMemory", typeof(InMemoryAiCallMetadataStore).Name, StringComparison.Ordinal);
    }

    [Fact]
    public void MetadataContract_CannotStoreRawPromptsInputsOutputsOrErrorDetails()
    {
        var properties = typeof(AiCallMetadata).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain(properties, name =>
            name.Contains("Input", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("PromptTemplate", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Output", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("ErrorMessage", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("StackTrace", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(nameof(AiCallMetadata.FailureCategory), properties);
        Assert.Contains(nameof(AiCallMetadata.DurationMilliseconds), properties);
        Assert.Contains(nameof(AiCallMetadata.TraceId), properties);
    }

    [Fact]
    public void AsyncParsingContracts_AreVersionedAndSuggestionOnly()
    {
        Assert.Equal("ai.parsing.requested.v1", AiParsingJobCommand.Name);
        Assert.Equal("ai.suggestion-ready.v1", AiSuggestionReadyIntegrationEvent.Name);
        Assert.Equal("ai.parsing-failed.v1", AiParsingFailedIntegrationEvent.Name);
        Assert.Equal(
            "ai.parsing-status-updated.v1",
            AiParsingStatusUpdatedIntegrationEvent.Name);

        Assert.Equal("queued", AiParsingJobStatuses.Queued);
        Assert.Equal("processing", AiParsingJobStatuses.Processing);
        Assert.Equal("suggestion_ready", AiParsingJobStatuses.SuggestionReady);
        Assert.Equal("failed", AiParsingJobStatuses.Failed);

        var contractTypes = new[]
        {
            typeof(AiParsingJobCommand),
            typeof(AiSuggestionReadyIntegrationEvent),
            typeof(AiParsingFailedIntegrationEvent),
            typeof(AiParsingStatusUpdatedIntegrationEvent)
        };
        foreach (var contractType in contractTypes)
        {
            var properties = contractType
                .GetProperties()
                .Select(property => property.Name)
                .ToArray();
            Assert.DoesNotContain(properties, name =>
                name.Equals("Input", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Prompt", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Output", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Confirmed", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("ErrorMessage", StringComparison.OrdinalIgnoreCase));
        }
    }
}
