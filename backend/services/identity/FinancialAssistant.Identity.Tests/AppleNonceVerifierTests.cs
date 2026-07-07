using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Identity.Infrastructure.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace FinancialAssistant.Identity.Tests;

public sealed class AppleNonceVerifierTests
{
    [Fact]
    public void Matches_AcceptsLowercaseHexSha256()
    {
        const string nonce = "synthetic-apple-nonce-value";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nonce)))
            .ToLowerInvariant();

        Assert.True(AppleNonceVerifier.Matches(nonce, hash));
    }

    [Fact]
    public void Matches_AcceptsBase64UrlSha256()
    {
        const string nonce = "synthetic-apple-nonce-value";
        var hash = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.UTF8.GetBytes(nonce)));

        Assert.True(AppleNonceVerifier.Matches(nonce, hash));
    }

    [Fact]
    public void Matches_RejectsMissingOrDifferentNonce()
    {
        Assert.False(AppleNonceVerifier.Matches("nonce-one-value", null));
        Assert.False(AppleNonceVerifier.Matches("nonce-one-value", "different-hash"));
    }
}
