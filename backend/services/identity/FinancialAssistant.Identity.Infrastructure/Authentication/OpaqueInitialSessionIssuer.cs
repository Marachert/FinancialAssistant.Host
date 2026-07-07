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
        string authenticationMethod,
        DateTimeOffset issuedAtUtc)
    {
        _ = issuedAtUtc;
        return sessionService
            .IssueAsync(account, client, authenticationMethod)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }
}
