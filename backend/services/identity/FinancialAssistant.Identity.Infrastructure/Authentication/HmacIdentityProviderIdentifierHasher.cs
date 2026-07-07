using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class HmacIdentityProviderIdentifierHasher : IIdentityProviderIdentifierHasher
{
    private readonly byte[] key;

    public HmacIdentityProviderIdentifierHasher(IOptions<IdentityServiceOptions> options)
    {
        var value = options.Value.Providers.IdentifierHmacKey;
        key = string.IsNullOrWhiteSpace(value)
            ? RandomNumberGenerator.GetBytes(32)
            : SHA256.HashData(Encoding.UTF8.GetBytes(value));
    }

    public string Hash(string provider, string identifierType, string value)
    {
        var input = string.Join(':', provider.ToLowerInvariant(), identifierType.ToLowerInvariant(), value);
        return Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(input)))
            .ToLowerInvariant();
    }
}
