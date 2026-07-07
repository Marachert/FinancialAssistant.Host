using System.Security.Cryptography;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Domain.Accounts;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class OpaqueInitialSessionIssuer : IInitialSessionIssuer
{
    private readonly IOptions<IdentityServiceOptions> options;

    public OpaqueInitialSessionIssuer(IOptions<IdentityServiceOptions> options)
    {
        this.options = options;
    }

    public AuthSessionResponse Issue(
        IdentityAccount account,
        IdentityClientContext client,
        DateTimeOffset issuedAtUtc)
    {
        _ = client;
        var authentication = options.Value.Authentication;
        var accessExpiresAt = issuedAtUtc.AddMinutes(authentication.AccessTokenLifetimeMinutes);
        var refreshExpiresAt = issuedAtUtc.AddDays(authentication.RefreshTokenLifetimeDays);
        var sessionId = Guid.NewGuid().ToString("N");
        var user = new CurrentUserContextResponse(
            account.Id,
            sessionId,
            account.Roles,
            "email_password",
            issuedAtUtc,
            refreshExpiresAt);

        return new AuthSessionResponse(
            "Bearer",
            CreateOpaqueValue(32),
            accessExpiresAt,
            CreateOpaqueValue(48),
            refreshExpiresAt,
            user);
    }

    private static string CreateOpaqueValue(int byteLength)
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(byteLength))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
