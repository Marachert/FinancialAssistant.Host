using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Monitoring.Contracts;

namespace FinancialAssistant.Monitoring.Api.Security;

public sealed class MonitoringGatewayAuthenticator
{
    public const string SharedSecretConfigurationKey = "Monitoring:Gateway:SharedSecret";
    private readonly byte[] sharedSecretHash;

    public MonitoringGatewayAuthenticator(IConfiguration configuration)
    {
        sharedSecretHash = ReadSecretHash(configuration, SharedSecretConfigurationKey);
    }

    public bool IsAuthenticated(HttpContext context) =>
        MonitoringSecretVerifier.Matches(
            context.Request.Headers[MonitoringHeaders.GatewayAuthentication],
            sharedSecretHash);

    public bool IsAdmin(HttpContext context) =>
        context.Request.Headers[MonitoringHeaders.GatewayRoles]
            .SelectMany(value => value?.Split(',', StringSplitOptions.TrimEntries) ?? [])
            .Any(role => string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase));

    internal static byte[] ReadSecretHash(IConfiguration configuration, string key)
    {
        var secret = configuration[key];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException($"{key} must contain at least 32 characters.");
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }
}

public sealed class MonitoringSignalAuthenticator
{
    public const string SharedSecretConfigurationKey = "Monitoring:Signals:SharedSecret";
    private readonly byte[] sharedSecretHash;

    public MonitoringSignalAuthenticator(IConfiguration configuration)
    {
        sharedSecretHash = MonitoringGatewayAuthenticator.ReadSecretHash(
            configuration,
            SharedSecretConfigurationKey);
    }

    public bool IsAuthenticated(HttpContext context) =>
        MonitoringSecretVerifier.Matches(
            context.Request.Headers[MonitoringHeaders.SignalAuthentication],
            sharedSecretHash);
}

internal static class MonitoringSecretVerifier
{
    public static bool Matches(
        Microsoft.Extensions.Primitives.StringValues values,
        byte[] expectedHash)
    {
        if (values.Count != 1)
        {
            return false;
        }

        var provided = values[0];
        return !string.IsNullOrEmpty(provided)
            && provided.Length <= 256
            && CryptographicOperations.FixedTimeEquals(
                expectedHash,
                SHA256.HashData(Encoding.UTF8.GetBytes(provided)));
    }
}
