using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.RecommendationsNotifications.Contracts;

namespace FinancialAssistant.RecommendationsNotifications.Api.Security;

public sealed class RecommendationNotificationGatewayAuthenticator
{
    public const string SharedSecretConfigurationKey =
        "RecommendationsNotifications:Gateway:SharedSecret";
    private readonly byte[] sharedSecretHash;

    public RecommendationNotificationGatewayAuthenticator(IConfiguration configuration)
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
        var values = context.Request.Headers[
            RecommendationNotificationGatewayHeaders.Authentication];
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
