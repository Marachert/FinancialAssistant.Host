using FinancialAssistant.Identity.Application.Sessions;

namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IIdentitySessionStore
{
    Task<bool> TryCreateAsync(
        IdentitySessionRecord session,
        CancellationToken cancellationToken = default);

    Task<IdentitySessionRecord?> FindByIdAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    Task<SessionRotationStoreResult> RotateAsync(
        string sessionId,
        string presentedRefreshTokenHash,
        IdentitySessionRecord replacement,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<SessionRevocationStoreResult> RevokeAsync(
        string sessionId,
        string accountId,
        string presentedRefreshTokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task RevokeFamilyAsync(
        string tokenFamilyIdHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
