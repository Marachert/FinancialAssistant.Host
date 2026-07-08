namespace FinancialAssistant.Identity.Application.Phone;

public sealed record PhoneVerificationChallengeRecord(
    string Id,
    string PhoneSubjectHash,
    string ClientInstanceHash,
    string Purpose,
    string? ProviderReference,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset ResendAvailableAtUtc,
    int FailedAttempts,
    PhoneVerificationChallengeStatus Status);

public enum PhoneVerificationChallengeStatus
{
    PendingDispatch = 1,
    Active = 2,
    Completed = 3,
    Locked = 4,
    Cancelled = 5
}
