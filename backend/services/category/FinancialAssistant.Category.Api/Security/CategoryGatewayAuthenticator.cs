using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Category.Contracts;

namespace FinancialAssistant.Category.Api.Security;

public sealed class CategoryGatewayAuthenticator
{
    public const string SharedSecretConfigurationKey = "Category:Gateway:SharedSecret";

    private readonly byte[] sharedSecretHash;

    public CategoryGatewayAuthenticator(IConfiguration configuration)
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
        var values = httpContext.Request.Headers[CategoryGatewayHeaders.Authentication];
        if (values.Count != 1)
        {
            return false;
        }

        var providedSecret = values[0];
        if (string.IsNullOrEmpty(providedSecret) || providedSecret.Length > 256)
        {
            return false;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedSecret));
        return CryptographicOperations.FixedTimeEquals(sharedSecretHash, providedHash);
    }
}
