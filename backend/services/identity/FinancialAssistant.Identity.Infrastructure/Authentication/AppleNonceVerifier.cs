using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public static class AppleNonceVerifier
{
    public static bool Matches(string rawNonce, string? tokenNonce)
    {
        if (string.IsNullOrWhiteSpace(rawNonce) || string.IsNullOrWhiteSpace(tokenNonce))
        {
            return false;
        }

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(rawNonce));
        var hexadecimal = Convert.ToHexString(digest).ToLowerInvariant();
        var base64Url = Base64UrlEncoder.Encode(digest);
        return FixedTimeEquals(tokenNonce, hexadecimal)
            || FixedTimeEquals(tokenNonce, base64Url);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.ASCII.GetBytes(left);
        var rightBytes = Encoding.ASCII.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
