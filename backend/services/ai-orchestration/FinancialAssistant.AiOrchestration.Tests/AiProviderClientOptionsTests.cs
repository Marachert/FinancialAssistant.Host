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
            Enabled = true,
            Mode = AiProviderClientOptions.SandboxMode,
            Name = "synthetic-provider",
            Model = "model-a",
            Endpoint = "https://provider.invalid/v1",
            CredentialEnvironmentVariable = "FINANCIAL_ASSISTANT_AI_PROVIDER_CREDENTIAL",
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
            Enabled = true,
            Mode = AiProviderClientOptions.SandboxMode,
            Name = "synthetic-provider",
            Model = "model-a",
            Endpoint = "https://api-key:secret@provider.invalid/v1",
            CredentialEnvironmentVariable = "FINANCIAL_ASSISTANT_AI_PROVIDER_CREDENTIAL",
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

    [Fact]
    public void EmptyDisabledPlaceholders_AreValidAndCannotCreateRoute()
    {
        var options = new AiProviderClientOptions();

        Assert.True(options.IsValidConfiguration);
        Assert.False(options.IsConfigured);
        Assert.Throws<InvalidOperationException>(() =>
            options.CreateRoute(TransactionParsingPromptCatalog.PromptName));
    }

    [Theory]
    [InlineData(false, "sandbox", "", "", "", "")]
    [InlineData(true, "disabled", "provider", "model", "https://provider.invalid", "AI_KEY")]
    [InlineData(true, "sandbox", "Provider", "model", "https://provider.invalid", "AI_KEY")]
    [InlineData(true, "production", "provider", "model", "http://provider.invalid", "AI_KEY")]
    [InlineData(true, "production", "provider", "model", "https://provider.invalid", "unsafe-key")]
    public void InvalidFeatureAndProviderCombinations_FailConfigurationValidation(
        bool enabled,
        string mode,
        string name,
        string model,
        string endpoint,
        string credentialEnvironmentVariable)
    {
        var options = new AiProviderClientOptions
        {
            Enabled = enabled,
            Mode = mode,
            Name = name,
            Model = model,
            Endpoint = endpoint,
            CredentialEnvironmentVariable = credentialEnvironmentVariable,
        };

        Assert.False(options.IsValidConfiguration);
        Assert.False(options.IsConfigured);
    }
}
