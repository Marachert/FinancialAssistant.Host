using FinancialAssistant.Identity.Application.Phone;

namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IPhoneVerificationChallengeStore
{
    Task<PhoneVerificationReservationResult> TryReserveAsync(
        PhoneVerificationChallengeRecord challenge,
        PhoneVerificationPolicy policy,
        CancellationToken cancellationToken = default);

    Task ActivateAsync(
        string verificationId,
        string providerReference,
        CancellationToken cancellationToken = default);

    Task CancelAsync(
        string verificationId,
        CancellationToken cancellationToken = default);

    Task<PhoneVerificationChallengeRecord?> FindAsync(
        string verificationId,
        CancellationToken cancellationToken = default);

    Task<PhoneVerificationAttemptResult> RecordRejectedAttemptAsync(
        string verificationId,
        int maximumAttempts,
        CancellationToken cancellationToken = default);

    Task<bool> TryCompleteAsync(
        string verificationId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed record PhoneVerificationReservationResult(
    bool Reserved,
    int? RetryAfterSeconds = null);

public sealed record PhoneVerificationAttemptResult(
    int FailedAttempts,
    bool Locked);
