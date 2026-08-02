using System.Security.Cryptography;
using System.Text;

namespace FinancialAssistant.RecommendationsNotifications.Application;

public static class RecommendationNotificationOwnerHasher
{
    public static string Hash(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User identifier is required.", nameof(userId));
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(userId.Trim()))).ToLowerInvariant();
    }
}
