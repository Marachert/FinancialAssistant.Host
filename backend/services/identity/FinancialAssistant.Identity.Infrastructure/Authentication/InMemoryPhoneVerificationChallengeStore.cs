using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Phone;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class InMemoryPhoneVerificationChallengeStore : IPhoneVerificationChallengeStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, PhoneVerificationChallengeRecord> challenges = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<StartEntry>> startsByPhone = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<StartEntry>> startsByClient = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> activeByPhone = new(StringComparer.Ordinal);

    public Task<PhoneVerificationReservationResult> TryReserveAsync(
        PhoneVerificationChallengeRecord challenge,
        PhoneVerificationPolicy policy,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var now = challenge.CreatedAtUtc;
            Prune(now - policy.StartWindow);

            if (activeByPhone.TryGetValue(challenge.PhoneSubjectHash, out var activeId)
                && challenges.TryGetValue(activeId, out var active))
            {
                if (active.ExpiresAtUtc <= now)
                {
                    challenges[activeId] = active with { Status = PhoneVerificationChallengeStatus.Cancelled };
                    activeByPhone.Remove(challenge.PhoneSubjectHash);
                }
                else if (active.Status == PhoneVerificationChallengeStatus.PendingDispatch)
                {
                    var retryAt = active.ResendAvailableAtUtc > now
                        ? active.ResendAvailableAtUtc
                        : Min(active.ExpiresAtUtc, now.Add(policy.ResendCooldown));
                    return Task.FromResult(new PhoneVerificationReservationResult(
                        false,
                        ToRetrySeconds(retryAt - now)));
                }
                else if (active.Status is PhoneVerificationChallengeStatus.Active
                    or PhoneVerificationChallengeStatus.Cancelled
                    && active.ResendAvailableAtUtc > now)
                {
                    return Task.FromResult(new PhoneVerificationReservationResult(
                        false,
                        ToRetrySeconds(active.ResendAvailableAtUtc - now)));
                }
            }

            var phoneStarts = GetEntries(startsByPhone, challenge.PhoneSubjectHash);
            if (phoneStarts.Count >= policy.MaximumStartsPerPhone)
            {
                return Task.FromResult(new PhoneVerificationReservationResult(
                    false,
                    ToRetrySeconds(phoneStarts[0].StartedAtUtc + policy.StartWindow - now)));
            }

            var clientStarts = GetEntries(startsByClient, challenge.ClientInstanceHash);
            if (clientStarts.Count >= policy.MaximumStartsPerClient)
            {
                return Task.FromResult(new PhoneVerificationReservationResult(
                    false,
                    ToRetrySeconds(clientStarts[0].StartedAtUtc + policy.StartWindow - now)));
            }

            if (activeByPhone.TryGetValue(challenge.PhoneSubjectHash, out activeId)
                && challenges.TryGetValue(activeId, out active)
                && active.Status != PhoneVerificationChallengeStatus.PendingDispatch)
            {
                challenges[activeId] = active with { Status = PhoneVerificationChallengeStatus.Cancelled };
            }

            challenges[challenge.Id] = challenge;
            activeByPhone[challenge.PhoneSubjectHash] = challenge.Id;
            phoneStarts.Add(new StartEntry(challenge.Id, now));
            clientStarts.Add(new StartEntry(challenge.Id, now));
            return Task.FromResult(new PhoneVerificationReservationResult(true));
        }
    }

    public Task<bool> TryActivateAsync(
        string verificationId,
        string providerReference,
        DateTimeOffset activatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!challenges.TryGetValue(verificationId, out var current)
                || current.Status != PhoneVerificationChallengeStatus.PendingDispatch
                || current.ExpiresAtUtc <= activatedAtUtc
                || !activeByPhone.TryGetValue(current.PhoneSubjectHash, out var activeId)
                || !string.Equals(activeId, verificationId, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            challenges[verificationId] = current with
            {
                ProviderReference = providerReference,
                Status = PhoneVerificationChallengeStatus.Active
            };
            return Task.FromResult(true);
        }
    }

    public Task CancelAsync(
        string verificationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (challenges.TryGetValue(verificationId, out var current))
            {
                challenges[verificationId] = current with { Status = PhoneVerificationChallengeStatus.Cancelled };
            }
        }

        return Task.CompletedTask;
    }

    public Task<PhoneVerificationChallengeRecord?> FindAsync(
        string verificationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            challenges.TryGetValue(verificationId, out var challenge);
            return Task.FromResult(challenge);
        }
    }

    public Task<PhoneVerificationAttemptResult> RecordRejectedAttemptAsync(
        string verificationId,
        int maximumAttempts,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!challenges.TryGetValue(verificationId, out var current))
            {
                return Task.FromResult(new PhoneVerificationAttemptResult(0, true));
            }

            var attempts = current.FailedAttempts + 1;
            var locked = attempts >= maximumAttempts;
            challenges[verificationId] = current with
            {
                FailedAttempts = attempts,
                Status = locked ? PhoneVerificationChallengeStatus.Locked : current.Status
            };
            if (locked)
            {
                activeByPhone.Remove(current.PhoneSubjectHash);
            }

            return Task.FromResult(new PhoneVerificationAttemptResult(attempts, locked));
        }
    }

    public Task<bool> TryCompleteAsync(
        string verificationId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!challenges.TryGetValue(verificationId, out var current)
                || current.Status != PhoneVerificationChallengeStatus.Active
                || current.ExpiresAtUtc <= completedAtUtc)
            {
                return Task.FromResult(false);
            }

            challenges[verificationId] = current with { Status = PhoneVerificationChallengeStatus.Completed };
            activeByPhone.Remove(current.PhoneSubjectHash);
            return Task.FromResult(true);
        }
    }

    private void Prune(DateTimeOffset threshold)
    {
        PruneEntries(startsByPhone, threshold);
        PruneEntries(startsByClient, threshold);
    }

    private static List<StartEntry> GetEntries(
        Dictionary<string, List<StartEntry>> source,
        string key)
    {
        if (!source.TryGetValue(key, out var entries))
        {
            entries = new List<StartEntry>();
            source[key] = entries;
        }

        return entries;
    }

    private static void PruneEntries(
        Dictionary<string, List<StartEntry>> source,
        DateTimeOffset threshold)
    {
        foreach (var entries in source.Values)
        {
            entries.RemoveAll(entry => entry.StartedAtUtc <= threshold);
        }
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) =>
        left <= right ? left : right;

    private static int ToRetrySeconds(TimeSpan delay) =>
        Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds));

    private sealed record StartEntry(string VerificationId, DateTimeOffset StartedAtUtc);
}
