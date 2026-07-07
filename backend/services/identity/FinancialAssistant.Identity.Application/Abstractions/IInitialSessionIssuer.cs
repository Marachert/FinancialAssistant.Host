using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Domain.Accounts;

namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IInitialSessionIssuer
{
    AuthSessionResponse Issue(
        IdentityAccount account,
        IdentityClientContext client,
        string authenticationMethod,
        DateTimeOffset issuedAtUtc);
}
