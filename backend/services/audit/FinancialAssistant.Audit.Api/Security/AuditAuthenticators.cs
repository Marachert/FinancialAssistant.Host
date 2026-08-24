using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Audit.Contracts;

namespace FinancialAssistant.Audit.Api.Security;

public sealed class AuditGatewayAuthenticator(IConfiguration configuration)
{
    public const string ConfigurationKey = "Audit:Gateway:SharedSecret";
    private readonly byte[] secretHash = AuditSecretVerifier.ReadHash(configuration, ConfigurationKey);

    public bool IsAuthenticated(HttpContext context) =>
        AuditSecretVerifier.Matches(
            context.Request.Headers[AuditHeaders.GatewayAuthentication],
            secretHash);

    public bool IsAdmin(HttpContext context) =>
        context.Request.Headers[AuditHeaders.GatewayRoles]
            .SelectMany(value => value?.Split(',', StringSplitOptions.TrimEntries) ?? [])
            .Any(role => string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase));
}

public sealed class AuditServiceAuthenticator(IConfiguration configuration)
{
    public const string ConfigurationKey = "Audit:Services:SharedSecret";
    private readonly byte[] secretHash = AuditSecretVerifier.ReadHash(configuration, ConfigurationKey);

    public bool IsAuthenticated(HttpContext context) =>
        AuditSecretVerifier.Matches(
            context.Request.Headers[AuditHeaders.ServiceAuthentication],
            secretHash);
}

internal static class AuditSecretVerifier
{
    public static byte[] ReadHash(IConfiguration configuration, string key)
    {
        var secret = configuration[key];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new InvalidOperationException($"{key} must contain at least 32 characters.");
        }

        return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

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
