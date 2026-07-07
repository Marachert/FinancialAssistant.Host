using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Application.Sessions;

public sealed partial class IdentitySessionService
{
    public async Task<IdentityOperationResult<bool>> LogoutAsync(
        LogoutRequest request,
        AccessSessionContext access,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var errors = IdentitySessionRequestValidator.Validate(request.RefreshToken, request.Client);
        if (errors.Count > 0)
        {
            return ValidationFailed<bool>("Logout request is invalid.", errors);
        }

        if (!renewalValues.TryReadSessionId(request.RefreshToken, out var sessionId)
            || !string.Equals(access.SessionId, sessionId, StringComparison.Ordinal))
        {
            return SessionFailed<bool>(IdentityErrorCodes.SessionInvalid, "The session could not be revoked.");
        }

        var now = clock.UtcNow;
        var current = await sessionStore.FindByIdAsync(access.SessionId, cancellationToken);
        if (current is null)
        {
            return SessionFailed<bool>(IdentityErrorCodes.SessionInvalid, "The session could not be revoked.");
        }

        var result = await sessionStore.RevokeAsync(
            access.SessionId,
            access.AccountId,
            renewalValues.Hash(request.RefreshToken),
            now,
            cancellationToken);

        if (result == SessionRevocationStoreResult.Success)
        {
            await PublishRevocationAsync(current, "logout", correlationId, now, cancellationToken);
            return IdentityOperationResult<bool>.Success(true);
        }

        return result switch
        {
            SessionRevocationStoreResult.Expired => SessionFailed<bool>(IdentityErrorCodes.SessionExpired, "The session has expired."),
            SessionRevocationStoreResult.Revoked => SessionFailed<bool>(IdentityErrorCodes.SessionRevoked, "The session has been revoked."),
            _ => SessionFailed<bool>(IdentityErrorCodes.SessionInvalid, "The session could not be revoked.")
        };
    }
}
