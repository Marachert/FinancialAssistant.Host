namespace FinancialAssistant.Identity.Application.Sessions;

public sealed record IdentitySessionRecord(
    string Id,
    string AccountId,
    string TokenFamilyIdHash,
    string RefreshTokenHash,
    string ClientInstanceIdHash,
    IdentitySessionStatus Status,
    string AuthenticationMethod,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset AccessTokenExpiresAtUtc,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    DateTimeOffset? RotatedAtUtc = null,
    DateTimeOffset? RevokedAtUtc = null,
    string? ReplacedBySessionId = null);

public enum IdentitySessionStatus
{
    Active = 1,
    Rotated = 2,
    Revoked = 3,
    Expired = 4
}
