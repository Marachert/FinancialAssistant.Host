using FinancialAssistant.Identity.Domain.Accounts;

namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IAccessTokenService
{
    AccessTokenIssueResult Issue(
        IdentityAccount account,
        string sessionId,
        string authenticationMethod,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset expiresAtUtc);
}

public sealed record AccessTokenIssueResult(string Token, DateTimeOffset ExpiresAtUtc);
