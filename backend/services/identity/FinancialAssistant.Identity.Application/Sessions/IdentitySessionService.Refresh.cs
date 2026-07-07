using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Application.Sessions;

public sealed partial class IdentitySessionService
{
    public async Task<IdentityOperationResult<AuthSessionResponse>> RefreshAsync(
        RefreshSessionRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var errors = IdentitySessionRequestValidator.Validate(request.RefreshToken, request.Client);
        if (errors.Count > 0)
        {
            return ValidationFailed<AuthSessionResponse>("Session renewal request is invalid.", errors);
        }

        if (!renewalValues.TryReadSessionId(request.RefreshToken, out var sessionId))
        {
            return SessionFailed<AuthSessionResponse>(IdentityErrorCodes.SessionInvalid, "The session could not be renewed.");
        }

        var current = await sessionStore.FindByIdAsync(sessionId, cancellationToken);
        if (current is null)
        {
            return SessionFailed<AuthSessionResponse>(IdentityErrorCodes.SessionInvalid, "The session could not be renewed.");
        }

        var account = await accountStore.FindAccountByIdAsync(current.AccountId, cancellationToken);
        if (account is null || !account.CanAuthenticate)
        {
            return SessionFailed<AuthSessionResponse>(IdentityErrorCodes.SessionInvalid, "The session could not be renewed.");
        }

        var now = clock.UtcNow;
        var replacementId = Guid.NewGuid().ToString("N");
        var replacementValue = renewalValues.Create(replacementId);
        var replacement = new IdentitySessionRecord(
            replacementId,
            current.AccountId,
            current.TokenFamilyIdHash,
            renewalValues.Hash(replacementValue),
            renewalValues.Hash($"client:{request.Client.ClientInstanceId}"),
            IdentitySessionStatus.Active,
            current.AuthenticationMethod,
            now,
            now.Add(policy.AccessLifetime),
            now.Add(policy.RenewalLifetime));

        var rotation = await sessionStore.RotateAsync(
            current.Id,
            renewalValues.Hash(request.RefreshToken),
            replacement,
            now,
            cancellationToken);

        if (rotation == SessionRotationStoreResult.Success)
        {
            return IdentityOperationResult<AuthSessionResponse>.Success(
                CreateResponse(account, replacement, replacementValue));
        }

        if (rotation == SessionRotationStoreResult.ReuseDetected)
        {
            await PublishRevocationAsync(current, "refresh_reuse", correlationId, now, cancellationToken);
            return SessionFailed<AuthSessionResponse>(IdentityErrorCodes.SessionRevoked, "The session has been revoked.");
        }

        return rotation switch
        {
            SessionRotationStoreResult.Expired => SessionFailed<AuthSessionResponse>(IdentityErrorCodes.SessionExpired, "The session has expired."),
            SessionRotationStoreResult.Revoked => SessionFailed<AuthSessionResponse>(IdentityErrorCodes.SessionRevoked, "The session has been revoked."),
            _ => SessionFailed<AuthSessionResponse>(IdentityErrorCodes.SessionInvalid, "The session could not be renewed.")
        };
    }
}
