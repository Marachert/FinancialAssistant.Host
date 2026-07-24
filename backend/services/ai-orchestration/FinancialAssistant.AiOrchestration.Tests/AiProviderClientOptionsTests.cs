using FinancialAssistant.AiOrchestration.Api.Configuration;
using FinancialAssistant.AiOrchestration.Infrastructure.Prompts;

namespace FinancialAssistant.AiOrchestration.Tests;

public sealed class AiProviderClientOptionsTests
{
    [Fact]
    public void ConfiguredOptions_CreateRuntimeRouteAndResilienceSettings()
    {
        var options = new AiProviderClientOptions
        {
            Name = "synthetic-provider",
            Model = "model-a",
            Endpoint = "https://provider.invalid/v1",
            RequestTimeoutSeconds = 45,
            MaximumAttempts = 2,
            RetryDelayMilliseconds = 500,
        };

        var route = options.CreateRoute(TransactionParsingPromptCatalog.PromptName);
        var resilience = options.CreateResilienceOptions();

        Assert.True(options.IsConfigured);
        Assert.Equal("synthetic-provider", route.Provider);
        Assert.Equal("model-a", route.Model);
        Assert.Equal(TimeSpan.FromSeconds(45), resilience.RequestTimeout);
        Assert.Equal(
            TransactionParsingPromptCatalog.ExecutionPolicy.MaximumAttempts,
            resilience.MaximumAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(500), resilience.RetryDelay);
    }

    [Fact]
    public void CredentialBearingProviderEndpoint_CannotCreateRoute()
    {
        var options = new AiProviderClientOptions
        {
            Name = "synthetic-provider",
            Model = "model-a",
            Endpoint = "https://api-key:secret@provider.invalid/v1",
        };

        Assert.True(options.HasAnyProviderIdentity);
        Assert.False(options.IsConfigured);
        Assert.Throws<InvalidOperationException>(() =>
            options.CreateRoute(TransactionParsingPromptCatalog.PromptName));
    }

    [Fact]
    public void AttemptsAbovePromptPolicy_CannotCreateResilienceSettings()
    {
        var options = new AiProviderClientOptions
        {
            MaximumAttempts =
                TransactionParsingPromptCatalog.ExecutionPolicy.MaximumAttempts + 1,
        };

        Assert.False(options.HasValidResilienceSettings);
        Assert.Throws<InvalidOperationException>(options.CreateResilienceOptions);
    }
}
