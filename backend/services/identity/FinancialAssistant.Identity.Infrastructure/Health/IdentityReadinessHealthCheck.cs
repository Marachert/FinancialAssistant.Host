using FinancialAssistant.Identity.Infrastructure.Configuration;
using FinancialAssistant.Identity.Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Identity.Infrastructure.Health;

public sealed class IdentityReadinessHealthCheck : IHealthCheck
{
    private readonly IOptions<IdentityServiceOptions> options;

    public IdentityReadinessHealthCheck(IOptions<IdentityServiceOptions> options)
    {
        this.options = options;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var configuration = options.Value;
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(configuration.ServiceName))
        {
            failures.Add("Identity service name is missing.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Storage.Provider))
        {
            failures.Add("Identity storage provider is missing.");
        }

        try
        {
            _ = IdentityIndexCatalog.Create(
                configuration.Storage.Environment,
                configuration.Storage.SchemaVersion,
                configuration.Storage.InitialGeneration);
        }
        catch (ArgumentException exception)
        {
            failures.Add($"Identity storage naming configuration is invalid: {exception.Message}");
        }

        var cleanup = configuration.Storage.Cleanup;
        if (cleanup.DeletedAccountRetentionDays < 1
            || cleanup.RemovedCredentialRetentionDays < 1
            || cleanup.TerminalSessionRetentionDays < 1
            || cleanup.HardMaximumSessionDocumentDays < cleanup.TerminalSessionRetentionDays
            || cleanup.RemovedProviderLinkRetentionDays < 1)
        {
            failures.Add("Identity cleanup retention configuration is invalid.");
        }

        var authentication = configuration.Authentication;
        if (!string.Equals(
                authentication.RuntimeAdapter,
                "InMemoryDevelopment",
                StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("Configured identity authentication runtime adapter is not supported by this increment.");
        }

        if (authentication.AccessTokenLifetimeMinutes < 1
            || authentication.RefreshTokenLifetimeDays < 1)
        {
            failures.Add("Identity session lifetime configuration is invalid.");
        }

        ValidateGoogleProvider(configuration.Providers, failures);
        ValidateAppleProvider(configuration.Providers, failures);
        ValidatePhoneProvider(configuration.Providers, failures);

        if (string.IsNullOrWhiteSpace(configuration.Events.Mode))
        {
            failures.Add("Identity event publishing mode is missing.");
        }

        if (string.Equals(configuration.Events.Mode, "RabbitMq", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(configuration.Events.Exchange))
        {
            failures.Add("Identity event exchange is required in RabbitMq mode.");
        }

        var result = failures.Count == 0
            ? HealthCheckResult.Healthy("Identity service registration configuration is ready.")
            : HealthCheckResult.Unhealthy(string.Join(" ", failures));

        return Task.FromResult(result);
    }

    private static void ValidateGoogleProvider(
        IdentityProviderOptions providers,
        List<string> failures)
    {
        var google = providers.Google;
        if (!google.Enabled)
        {
            return;
        }

        if (google.ClientIds.Count == 0 || google.ClientIds.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("At least one Google OAuth client ID is required when Google sign-in is enabled.");
        }

        if (string.IsNullOrWhiteSpace(providers.IdentifierHmacKey))
        {
            failures.Add("Provider identifier HMAC key is required when Google sign-in is enabled.");
        }

        if (google.IssuedAtClockToleranceSeconds < 0
            || google.ExpirationClockToleranceSeconds < 0)
        {
            failures.Add("Google token clock tolerance configuration is invalid.");
        }
    }

    private static void ValidateAppleProvider(
        IdentityProviderOptions providers,
        List<string> failures)
    {
        var apple = providers.Apple;
        if (!apple.Enabled)
        {
            return;
        }

        if (apple.ClientIds.Count == 0 || apple.ClientIds.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add("At least one Apple client ID is required when Apple sign-in is enabled.");
        }

        if (string.IsNullOrWhiteSpace(providers.IdentifierHmacKey))
        {
            failures.Add("Provider identifier HMAC key is required when Apple sign-in is enabled.");
        }

        if (!Uri.TryCreate(apple.Issuer, UriKind.Absolute, out var issuer)
            || issuer.Scheme != Uri.UriSchemeHttps
            || !Uri.TryCreate(apple.DiscoveryEndpoint, UriKind.Absolute, out var discovery)
            || discovery.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add("Apple issuer and discovery endpoint must be valid HTTPS URLs.");
        }

        if (apple.ClockSkewSeconds < 0)
        {
            failures.Add("Apple token clock skew configuration is invalid.");
        }

        if (!apple.RequireNonce)
        {
            failures.Add("Apple sign-in nonce validation must remain enabled.");
        }
    }

    private static void ValidatePhoneProvider(
        IdentityProviderOptions providers,
        List<string> failures)
    {
        var phone = providers.Phone;
        if (!phone.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(providers.IdentifierHmacKey))
        {
            failures.Add("Provider identifier HMAC key is required when phone verification is enabled.");
        }

        if (string.IsNullOrWhiteSpace(phone.Adapter)
            || string.Equals(phone.Adapter, "Disabled", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("A phone verification provider adapter is required when phone verification is enabled.");
        }

        if (phone.CodeLength is < 4 or > 10
            || phone.ChallengeLifetimeMinutes is < 1 or > 30
            || phone.ResendCooldownSeconds is < 10 or > 600
            || phone.MaximumAttempts is < 1 or > 10
            || phone.StartWindowMinutes is < 1 or > 1440
            || phone.MaximumStartsPerPhone is < 1 or > 100
            || phone.MaximumStartsPerClient is < 1 or > 500)
        {
            failures.Add("Phone verification retry, cooldown, or challenge policy is invalid.");
        }
    }
}
