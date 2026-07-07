using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Sessions;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Domain.Accounts;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class OpaqueInitialSessionIssuer : IInitialSessionIssuer
{
    private readonly IIdentitySessionService sessionService;

    public OpaqueInitialSessionIssuer(IIdentitySessionService sessionService)
    {
        this.sessionService = sessionService;
    }

    public AuthSessionResponse Issue(
        IdentityAccount account,
        IdentityClientContext client,
        DateTimeOffset issuedAtUtc)
    {
        _ = issuedAtUtc;
        return sessionService
            .IssueAsync(account, client, "email_password")
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }
}
