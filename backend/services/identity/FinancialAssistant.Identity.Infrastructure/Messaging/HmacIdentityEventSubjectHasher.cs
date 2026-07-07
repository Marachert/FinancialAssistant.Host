using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Identity.Infrastructure.Messaging;

public sealed class HmacIdentityEventSubjectHasher : IIdentityEventSubjectHasher
{
    private readonly byte[] key;

    public HmacIdentityEventSubjectHasher(IOptions<IdentityServiceOptions> options)
    {
        var configured = options.Value.Events.UserIdHmacKey;
        key = string.IsNullOrWhiteSpace(configured)
            ? RandomNumberGenerator.GetBytes(32)
            : SHA256.HashData(Encoding.UTF8.GetBytes(configured));
    }

    public string Hash(string subjectId)
    {
        var digest = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(subjectId));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }
}
