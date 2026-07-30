using FinancialAssistant.AiOrchestration.Application.Abstractions;
using FinancialAssistant.AiOrchestration.Domain;
using FinancialAssistant.AiOrchestration.Infrastructure.Prompts;
using FinancialAssistant.AiOrchestration.Infrastructure.Providers;

namespace FinancialAssistant.AiOrchestration.Api.Configuration;

public sealed class AiOrchestrationOptions
{
    public const string SectionName = "AiOrchestration";
    public const string SuggestionAuthority = "suggestion";

    public string Name { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string OutputAuthority { get; init; } = SuggestionAuthority;

    public AiProviderClientOptions Provider { get; init; } = new();
}

public sealed class AiProviderClientOptions
{
    public const string DisabledMode = "disabled";
    public const string SandboxMode = "sandbox";
    public const string ProductionMode = "production";

    public bool Enabled { get; init; }

    public string Mode { get; init; } = DisabledMode;

    public string Name { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string Endpoint { get; init; } = string.Empty;

    public string CredentialEnvironmentVariable { get; init; } = string.Empty;

    public int RequestTimeoutSeconds { get; init; } = 30;

    public int MaximumAttempts { get; init; } =
        TransactionParsingPromptCatalog.ExecutionPolicy.MaximumAttempts;

    public int RetryDelayMilliseconds { get; init; } = 200;

    public AiUsageCostControlOptions UsageCostControls { get; init; } = new();

    public bool HasAnyProviderIdentity =>
        !string.IsNullOrWhiteSpace(Name) ||
        !string.IsNullOrWhiteSpace(Model) ||
        !string.IsNullOrWhiteSpace(Endpoint);

    public bool IsConfigured =>
        Enabled &&
        IsEnabledMode(Mode) &&
        IsSafeIdentifier(Name) &&
        IsSafeIdentifier(Model) &&
        Uri.TryCreate(Endpoint, UriKind.Absolute, out var endpoint) &&
        endpoint.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(endpoint.UserInfo) &&
        IsSafeEnvironmentVariableName(CredentialEnvironmentVariable);

    public bool IsValidConfiguration =>
        IsConfigured ||
        (!Enabled &&
         string.Equals(Mode, DisabledMode, StringComparison.Ordinal) &&
         !HasAnyProviderIdentity &&
         string.IsNullOrWhiteSpace(CredentialEnvironmentVariable));

    public bool HasValidResilienceSettings =>
        RequestTimeoutSeconds is >= 1 and <= 120 &&
        MaximumAttempts >= 1 &&
        MaximumAttempts <= TransactionParsingPromptCatalog.ExecutionPolicy.MaximumAttempts &&
        RetryDelayMilliseconds is >= 0 and <= 5000;

    public AiModelRoute CreateRoute(string capabilityName)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "The AI provider must be enabled with a valid mode, identity, HTTPS endpoint, and credential reference.");
        }

        return new AiModelRoute(capabilityName, Name, Model);
    }

    public LlmProviderResilienceOptions CreateResilienceOptions()
    {
        if (!HasValidResilienceSettings)
        {
            throw new InvalidOperationException(
                "Provider timeout and retry settings are outside the allowed range.");
        }

        return new LlmProviderResilienceOptions(
            TimeSpan.FromSeconds(RequestTimeoutSeconds),
            MaximumAttempts,
            TimeSpan.FromMilliseconds(RetryDelayMilliseconds));
    }

    private static bool IsEnabledMode(string mode) =>
        string.Equals(mode, SandboxMode, StringComparison.Ordinal) ||
        string.Equals(mode, ProductionMode, StringComparison.Ordinal);

    private static bool IsSafeIdentifier(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 64 &&
        value.All(character =>
            character is >= 'a' and <= 'z' ||
            character is >= '0' and <= '9' ||
            character is '.' or '_' or '-');

    private static bool IsSafeEnvironmentVariableName(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 128 &&
        value[0] is >= 'A' and <= 'Z' &&
        value.All(character =>
            character is >= 'A' and <= 'Z' ||
            character is >= '0' and <= '9' ||
            character == '_');
}

public sealed class AiUsageCostControlOptions
{
    public const int DefaultPerUserDailyRequestLimit = 20;
    public const int DefaultMaximumRequestCharacters = 8_000;
    public const decimal DefaultMonthlyBudgetAlertUsd = 25m;

    public int PerUserDailyRequestLimit { get; init; } =
        DefaultPerUserDailyRequestLimit;

    public int MaximumRequestCharacters { get; init; } =
        DefaultMaximumRequestCharacters;

    public decimal MonthlyBudgetAlertUsd { get; init; } =
        DefaultMonthlyBudgetAlertUsd;

    public bool AdminVisibilityEnabled { get; init; } = true;

    public bool IsValid =>
        PerUserDailyRequestLimit is >= 1 and <= 10_000 &&
        MaximumRequestCharacters is >= 1 and <= 100_000 &&
        MonthlyBudgetAlertUsd is >= 1 and <= 1_000_000 &&
        AdminVisibilityEnabled;

    public AiUsageCostControlPolicy CreatePolicy()
    {
        if (!IsValid)
        {
            throw new InvalidOperationException(
                "AI usage cost-control settings are outside the allowed range.");
        }

        return new AiUsageCostControlPolicy(
            PerUserDailyRequestLimit,
            MaximumRequestCharacters,
            MonthlyBudgetAlertUsd,
            AdminVisibilityEnabled);
    }
}
