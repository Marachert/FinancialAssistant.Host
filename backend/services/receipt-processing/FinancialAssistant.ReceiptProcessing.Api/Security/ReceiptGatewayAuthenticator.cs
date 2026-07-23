using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.ReceiptProcessing.Contracts;

namespace FinancialAssistant.ReceiptProcessing.Api.Security;

public sealed class ReceiptGatewayAuthenticator
{
    public const string SharedSecretConfigurationKey = "ReceiptProcessing:Gateway:SharedSecret";

    private readonly byte[] sharedSecretHash;

    public ReceiptGatewayAuthenticator(IConfiguration configuration)
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
        var values = context.Request.Headers[ReceiptProcessingHeaders.GatewayAuthentication];
        if (values.Count != 1)
        {
            return false;
        }

        var provided = values[0];
        if (string.IsNullOrEmpty(provided) || provided.Length > 256)
        {
            return false;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        return CryptographicOperations.FixedTimeEquals(sharedSecretHash, providedHash);
    }
}
