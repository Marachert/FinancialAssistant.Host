using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Analytics.Contracts;

namespace FinancialAssistant.Analytics.Api.Security;

public sealed class AnalyticsGatewayAuthenticator
{
    public const string SharedSecretConfigurationKey = "Analytics:Gateway:SharedSecret";
    private readonly byte[] sharedSecretHash;

    public AnalyticsGatewayAuthenticator(IConfiguration configuration)
    {
        var sharedSecret = configuration[SharedSecretConfigurationKey];
        if (string.IsNullOrWhiteSpace(sharedSecret) || sharedSecret.Length < 32)
        {
            throw new InvalidOperationException(
                $"{SharedSecretConfigurationKey} must contain at least 32 characters.");
        }

        sharedSecretHash = SHA256.HashData(Encoding.UTF8.GetBytes(sharedSecret));
    }

    public bool IsAuthenticated(HttpContext httpContext)
    {
        var values = httpContext.Request.Headers[AnalyticsGatewayHeaders.Authentication];
        if (values.Count != 1)
        {
            return false;
        }

        var provided = values[0];
        if (string.IsNullOrEmpty(provided) || provided.Length > 256)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            sharedSecretHash,
            SHA256.HashData(Encoding.UTF8.GetBytes(provided)));
    }
}
