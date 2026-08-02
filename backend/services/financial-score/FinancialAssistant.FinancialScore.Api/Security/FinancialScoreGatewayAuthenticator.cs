using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.FinancialScore.Contracts;

namespace FinancialAssistant.FinancialScore.Api.Security;

public sealed class FinancialScoreGatewayAuthenticator
{
    public const string SharedSecretConfigurationKey = "FinancialScore:Gateway:SharedSecret";
    private readonly byte[] sharedSecretHash;

    public FinancialScoreGatewayAuthenticator(IConfiguration configuration)
    {
        var sharedSecret = configuration[SharedSecretConfigurationKey];
        if (string.IsNullOrWhiteSpace(sharedSecret) || sharedSecret.Length < 32)
        {
            throw new InvalidOperationException(
                $"{SharedSecretConfigurationKey} must contain at least 32 characters.");
        }

        sharedSecretHash = SHA256.HashData(Encoding.UTF8.GetBytes(sharedSecret));
    }

    public bool IsAuthenticated(HttpContext context)
    {
        var values = context.Request.Headers[FinancialScoreGatewayHeaders.Authentication];
        if (values.Count != 1)
        {
            return false;
        }

        var provided = values[0];
        return !string.IsNullOrEmpty(provided) &&
            provided.Length <= 256 &&
            CryptographicOperations.FixedTimeEquals(
                sharedSecretHash,
                SHA256.HashData(Encoding.UTF8.GetBytes(provided)));
    }
}
