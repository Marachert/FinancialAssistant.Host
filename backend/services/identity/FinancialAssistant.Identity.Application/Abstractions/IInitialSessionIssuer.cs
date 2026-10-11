using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Domain.Accounts;

namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IInitialSessionIssuer
{
    Task<AuthSessionResponse> IssueAsync(
        IdentityAccount account,
        IdentityClientContext client,
        string authenticationMethod,
        DateTimeOffset issuedAtUtc,
        CancellationToken cancellationToken = default);
}
