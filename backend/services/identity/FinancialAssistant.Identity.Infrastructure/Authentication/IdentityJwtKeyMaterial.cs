using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class IdentityJwtKeyMaterial
{
    public IdentityJwtKeyMaterial(IdentityAuthenticationOptions options)
    {
        Issuer = options.AccessTokenIssuer;
        Audience = options.AccessTokenAudience;
        var keyBytes = string.IsNullOrWhiteSpace(options.AccessTokenSigningKey)
            ? RandomNumberGenerator.GetBytes(64)
            : SHA512.HashData(Encoding.UTF8.GetBytes(options.AccessTokenSigningKey));
        SigningKey = new SymmetricSecurityKey(keyBytes);
    }

    public string Issuer { get; }

    public string Audience { get; }

    public SymmetricSecurityKey SigningKey { get; }

    public TokenValidationParameters CreateValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKey,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }
}
