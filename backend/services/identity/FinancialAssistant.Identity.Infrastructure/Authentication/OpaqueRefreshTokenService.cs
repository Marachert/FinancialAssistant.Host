using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class OpaqueRefreshTokenService : IRefreshTokenService
{
    private const string Prefix = "rt1";
    private readonly byte[] hashKey;

    public OpaqueRefreshTokenService(IOptions<IdentityServiceOptions> options)
    {
        var configured = options.Value.Authentication.RefreshTokenHashKey;
        hashKey = string.IsNullOrWhiteSpace(configured)
            ? RandomNumberGenerator.GetBytes(32)
            : SHA256.HashData(Encoding.UTF8.GetBytes(configured));
    }

    public string Create(string sessionId)
    {
        var secret = Base64Url(RandomNumberGenerator.GetBytes(48));
        return $"{Prefix}.{sessionId}.{secret}";
    }

    public bool TryReadSessionId(string refreshToken, out string sessionId)
    {
        sessionId = string.Empty;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return false;
        }

        var parts = refreshToken.Split('.', StringSplitOptions.None);
        if (parts.Length != 3
            || !string.Equals(parts[0], Prefix, StringComparison.Ordinal)
            || parts[1].Length != 32
            || !Guid.TryParseExact(parts[1], "N", out _)
            || parts[2].Length < 43)
        {
            return false;
        }

        sessionId = parts[1];
        return true;
    }

    public string Hash(string refreshToken)
    {
        var digest = HMACSHA256.HashData(hashKey, Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
