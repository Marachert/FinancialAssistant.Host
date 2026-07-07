using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class HmacEmailLookupHasher : IEmailLookupHasher
{
    private readonly byte[] key;

    public HmacEmailLookupHasher(IOptions<IdentityServiceOptions> options)
    {
        var configuredKey = options.Value.Authentication.LookupHmacKey;
        key = string.IsNullOrWhiteSpace(configuredKey)
            ? RandomNumberGenerator.GetBytes(32)
            : SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
    }

    public string Hash(string normalizedEmail)
    {
        var digest = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(normalizedEmail));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
