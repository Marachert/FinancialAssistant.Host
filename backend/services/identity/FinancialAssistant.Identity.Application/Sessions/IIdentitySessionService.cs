using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Domain.Accounts;

namespace FinancialAssistant.Identity.Application.Sessions;

public interface IIdentitySessionService
{
    Task<AuthSessionResponse> IssueAsync(
        IdentityAccount account,
        IdentityClientContext client,
        string authenticationMethod,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult<AuthSessionResponse>> RefreshAsync(
        RefreshSessionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult<bool>> LogoutAsync(
        LogoutRequest request,
        AccessSessionContext access,
        string correlationId,
        CancellationToken cancellationToken = default);

    Task<IdentityOperationResult<CurrentUserContextResponse>> GetCurrentAsync(
        AccessSessionContext access,
        CancellationToken cancellationToken = default);
}
