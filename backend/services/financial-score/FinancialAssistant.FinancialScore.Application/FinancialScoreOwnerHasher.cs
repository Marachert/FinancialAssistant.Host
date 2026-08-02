using System.Security.Cryptography;
using System.Text;

namespace FinancialAssistant.FinancialScore.Application;

public static class FinancialScoreOwnerHasher
{
    public static string Hash(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(userId.Trim())))
            .ToLowerInvariant();
    }
}
