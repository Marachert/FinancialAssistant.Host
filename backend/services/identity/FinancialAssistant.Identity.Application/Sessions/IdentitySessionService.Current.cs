using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Application.Sessions;

public sealed partial class IdentitySessionService
{
    public async Task<IdentityOperationResult<CurrentUserContextResponse>> GetCurrentAsync(
        AccessSessionContext access,
        CancellationToken cancellationToken = default)
    {
        var session = await sessionStore.FindByIdAsync(access.SessionId, cancellationToken);
        var account = await accountStore.FindAccountByIdAsync(access.AccountId, cancellationToken);
        if (session is null
            || account is null
            || !account.CanAuthenticate
            || !string.Equals(session.AccountId, account.Id, StringComparison.Ordinal)
            || session.Status is IdentitySessionStatus.Revoked or IdentitySessionStatus.Expired
            || session.RefreshTokenExpiresAtUtc <= clock.UtcNow)
        {
            return SessionFailed<CurrentUserContextResponse>(
                IdentityErrorCodes.SessionRevoked,
                "The current session is not valid.");
        }

        return IdentityOperationResult<CurrentUserContextResponse>.Success(
            new CurrentUserContextResponse(
                account.Id,
                session.Id,
                account.Roles,
                access.AuthenticationMethod,
                access.AuthenticatedAtUtc,
                session.RefreshTokenExpiresAtUtc));
    }
}
