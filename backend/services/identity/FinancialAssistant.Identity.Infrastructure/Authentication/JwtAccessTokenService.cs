using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Domain.Accounts;
using Microsoft.IdentityModel.Tokens;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class JwtAccessTokenService : IAccessTokenService
{
    private readonly IdentityJwtKeyMaterial keyMaterial;
    private readonly JwtSecurityTokenHandler handler = new();

    public JwtAccessTokenService(IdentityJwtKeyMaterial keyMaterial)
    {
        this.keyMaterial = keyMaterial;
    }

    public AccessTokenIssueResult Issue(
        IdentityAccount account,
        string sessionId,
        string authenticationMethod,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id),
            new("sid", sessionId),
            new("amr", authenticationMethod),
            new(JwtRegisteredClaimNames.Iat, issuedAtUtc.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        claims.AddRange(account.Roles.Select(role => new Claim("role", role)));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = keyMaterial.Issuer,
            Audience = keyMaterial.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = issuedAtUtc.UtcDateTime,
            IssuedAt = issuedAtUtc.UtcDateTime,
            Expires = expiresAtUtc.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                keyMaterial.SigningKey,
                SecurityAlgorithms.HmacSha256)
        };

        var token = handler.CreateToken(descriptor);
        return new AccessTokenIssueResult(handler.WriteToken(token), expiresAtUtc);
    }
}
