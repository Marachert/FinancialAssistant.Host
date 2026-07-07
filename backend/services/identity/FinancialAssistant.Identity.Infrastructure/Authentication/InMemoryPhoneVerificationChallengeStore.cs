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
            Prune(challenge.CreatedAtUtc - policy.StartWindow);

            if (activeByPhone.TryGetValue(challenge.PhoneSubjectHash, out var activeId)
                && challenges.TryGetValue(activeId, out var active)
                && active.Status is PhoneVerificationChallengeStatus.PendingDispatch or PhoneVerificationChallengeStatus.Active
                && active.ResendAvailableAtUtc > challenge.CreatedAtUtc)
            {
                return Task.FromResult(new PhoneVerificationReservationResult(
                    false,
                    ToRetrySeconds(active.ResendAvailableAtUtc - challenge.CreatedAtUtc)));
            }

            var phoneStarts = GetEntries(startsByPhone, challenge.PhoneSubjectHash);
            if (phoneStarts.Count >= policy.MaximumStartsPerPhone)
            {
                return Task.FromResult(new PhoneVerificationReservationResult(
                    false,
                    ToRetrySeconds(phoneStarts[0].StartedAtUtc + policy.StartWindow - challenge.CreatedAtUtc)));
            }

            var clientStarts = GetEntries(startsByClient, challenge.ClientInstanceHash);
            if (clientStarts.Count >= policy.MaximumStartsPerClient)
            {
                return Task.FromResult(new PhoneVerificationReservationResult(
                    false,
                    ToRetrySeconds(clientStarts[0].StartedAtUtc + policy.StartWindow - challenge.CreatedAtUtc)));
            }

            if (activeByPhone.TryGetValue(challenge.PhoneSubjectHash, out activeId)
                && challenges.TryGetValue(activeId, out active)
                && active.Status is PhoneVerificationChallengeStatus.PendingDispatch or PhoneVerificationChallengeStatus.Active)
            {
                challenges[activeId] = active with { Status = PhoneVerificationChallengeStatus.Cancelled };
            }

            challenges[challenge.Id] = challenge;
            activeByPhone[challenge.PhoneSubjectHash] = challenge.Id;
            phoneStarts.Add(new StartEntry(challenge.Id, challenge.CreatedAtUtc));
            clientStarts.Add(new StartEntry(challenge.Id, challenge.CreatedAtUtc));
            return Task.FromResult(new PhoneVerificationReservationResult(true));
        }
    }

    public Task ActivateAsync(
        string verificationId,
        string providerReference,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (challenges.TryGetValue(verificationId, out var current)
                && current.Status == PhoneVerificationChallengeStatus.PendingDispatch)
            {
                challenges[verificationId] = current with
                {
                    ProviderReference = providerReference,
                    Status = PhoneVerificationChallengeStatus.Active
                };
            }
        }

        return Task.CompletedTask;
    }

    public Task CancelAsync(
        string verificationId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!challenges.TryGetValue(verificationId, out var current))
            {
                return Task.CompletedTask;
            }

            challenges[verificationId] = current with { Status = PhoneVerificationChallengeStatus.Cancelled };
            if (activeByPhone.TryGetValue(current.PhoneSubjectHash, out var activeId)
                && string.Equals(activeId, verificationId, StringComparison.Ordinal))
            {
                activeByPhone.Remove(current.PhoneSubjectHash);
            }

            RemoveStart(startsByPhone, current.PhoneSubjectHash, verificationId);
            RemoveStart(startsByClient, current.ClientInstanceHash, verificationId);
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

    private static void RemoveStart(
        Dictionary<string, List<StartEntry>> source,
        string key,
        string verificationId)
    {
        if (source.TryGetValue(key, out var entries))
        {
            entries.RemoveAll(entry => string.Equals(entry.VerificationId, verificationId, StringComparison.Ordinal));
        }
    }

    private static int ToRetrySeconds(TimeSpan delay) =>
        Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds));

    private sealed record StartEntry(string VerificationId, DateTimeOffset StartedAtUtc);
}
