using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.ReceiptProcessing.Contracts;

namespace FinancialAssistant.TransactionIntake.Api.Security;

public sealed class ReceiptEventAuthenticator
{
    public const string SharedSecretConfigurationKey =
        "ReceiptProcessing:Events:SharedSecret";

    private readonly byte[] sharedSecretHash;

    public ReceiptEventAuthenticator(IConfiguration configuration)
    {
        var sharedSecret = configuration[SharedSecretConfigurationKey];
        if (string.IsNullOrWhiteSpace(sharedSecret) ||
            sharedSecret.Length is < 32 or > 256)
        {
            throw new InvalidOperationException(
                $"{SharedSecretConfigurationKey} must contain 32 to 256 characters.");
        }

        sharedSecretHash = SHA256.HashData(Encoding.UTF8.GetBytes(sharedSecret));
    }

    public bool IsAuthenticated(HttpContext httpContext)
    {
        var values = httpContext.Request.Headers[
            ReceiptProcessingHeaders.EventAuthentication];
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
        return CryptographicOperations.FixedTimeEquals(
            sharedSecretHash,
            providedHash);
    }
}
