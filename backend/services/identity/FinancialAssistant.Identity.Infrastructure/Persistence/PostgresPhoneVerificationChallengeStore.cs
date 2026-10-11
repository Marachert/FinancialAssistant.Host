using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Phone;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Identity.Infrastructure.Persistence;

public sealed class PostgresPhoneVerificationChallengeStore : IPhoneVerificationChallengeStore
{
    private const string Owner = "identity";
    private readonly IdentityPersistenceContext database;
    public PostgresPhoneVerificationChallengeStore(IdentityPersistenceContext database) { this.database = database; }

    public Task<PhoneVerificationReservationResult> TryReserveAsync(PhoneVerificationChallengeRecord challenge,
        PhoneVerificationPolicy policy, CancellationToken cancellationToken = default) => database.WithRecordsAsync(async (records, _) =>
        {
            Validate(challenge);
            IdentityRecordValidation.Require(challenge.Status == PhoneVerificationChallengeStatus.PendingDispatch
                && challenge.ProviderReference is null && challenge.FailedAttempts == 0
                && policy.MaximumAttempts is >= 1 and <= 10 && policy.MaximumStartsPerPhone is >= 1 and <= 100
                && policy.MaximumStartsPerClient is >= 1 and <= 500 && policy.StartWindow > TimeSpan.Zero
                && policy.StartWindow <= TimeSpan.FromDays(1) && policy.ResendCooldown > TimeSpan.Zero);
            if (await records.GetAsync("phone_challenges", Owner, challenge.Id) is not null) { return new PhoneVerificationReservationResult(false); }
            var now = challenge.CreatedAtUtc;
            var phone = await IdentityPersistenceContext.ReadAsync<PhoneState>(records, "phone_state", Owner, challenge.PhoneSubjectHash)
                ?? new PhoneState(null, Array.Empty<DateTimeOffset>());
            var client = await IdentityPersistenceContext.ReadAsync<ClientState>(records, "phone_clients", Owner, challenge.ClientInstanceHash)
                ?? new ClientState(Array.Empty<DateTimeOffset>());
            var active = phone.ActiveId is null ? null : await ReadAsync(records, phone.ActiveId);
            IdentityRecordValidation.Require(phone.Starts is { Length: <= 100 } && client.Starts is { Length: <= 500 }
                && (phone.ActiveId is null || active is not null && active.PhoneSubjectHash == challenge.PhoneSubjectHash));
            if (active is not null)
            {
                if (active.ExpiresAtUtc <= now)
                {
                    await SaveAsync(records, active with { Status = PhoneVerificationChallengeStatus.Cancelled });
                    phone = phone with { ActiveId = null };
                }
                else if (active.Status == PhoneVerificationChallengeStatus.PendingDispatch)
                {
                    var retry = active.ResendAvailableAtUtc > now ? active.ResendAvailableAtUtc :
                        (active.ExpiresAtUtc < now + policy.ResendCooldown ? active.ExpiresAtUtc : now + policy.ResendCooldown);
                    return new PhoneVerificationReservationResult(false, Retry(retry - now));
                }
                else if (active.Status is PhoneVerificationChallengeStatus.Active or PhoneVerificationChallengeStatus.Cancelled && active.ResendAvailableAtUtc > now)
                {
                    return new PhoneVerificationReservationResult(false, Retry(active.ResendAvailableAtUtc - now));
                }
            }
            var phoneStarts = phone.Starts.Where(time => time > now - policy.StartWindow).Order().ToArray();
            var clientStarts = client.Starts.Where(time => time > now - policy.StartWindow).Order().ToArray();
            if (phoneStarts.Length >= policy.MaximumStartsPerPhone) { return new PhoneVerificationReservationResult(false, Retry(phoneStarts[0] + policy.StartWindow - now)); }
            if (clientStarts.Length >= policy.MaximumStartsPerClient) { return new PhoneVerificationReservationResult(false, Retry(clientStarts[0] + policy.StartWindow - now)); }
            if (active is not null && active.Status != PhoneVerificationChallengeStatus.PendingDispatch)
            {
                await SaveAsync(records, active with { Status = PhoneVerificationChallengeStatus.Cancelled });
            }
            await records.InsertAsync("phone_challenges", Owner, challenge.Id, IdentityPersistenceContext.Encode(challenge));
            await IdentityPersistenceContext.WriteAsync(records, "phone_state", Owner, challenge.PhoneSubjectHash,
                new PhoneState(challenge.Id, phoneStarts.Append(now).ToArray()));
            await IdentityPersistenceContext.WriteAsync(records, "phone_clients", Owner, challenge.ClientInstanceHash,
                new ClientState(clientStarts.Append(now).ToArray()));
            return new PhoneVerificationReservationResult(true);
        }, cancellationToken);

    public Task<bool> TryActivateAsync(string verificationId, string providerReference, DateTimeOffset activatedAtUtc,
        CancellationToken cancellationToken = default) => database.WithRecordsAsync(async (records, _) =>
        {
            var current = await ReadAsync(records, verificationId);
            if (current is null || current.Status != PhoneVerificationChallengeStatus.PendingDispatch || current.ExpiresAtUtc <= activatedAtUtc) { return false; }
            var phone = await IdentityPersistenceContext.ReadAsync<PhoneState>(records, "phone_state", Owner, current.PhoneSubjectHash);
            if (phone?.ActiveId != verificationId) { return false; }
            if (string.IsNullOrWhiteSpace(providerReference) || providerReference.Length > 512) { throw new ArgumentException("Phone provider reference is invalid."); }
            await SaveAsync(records, current with { ProviderReference = providerReference, Status = PhoneVerificationChallengeStatus.Active });
            return true;
        }, cancellationToken);

    public Task CancelAsync(string verificationId, CancellationToken cancellationToken = default) => database.WithRecordsAsync(async (records, _) =>
        {
            var current = await ReadAsync(records, verificationId);
            if (current is not null && current.Status is PhoneVerificationChallengeStatus.PendingDispatch or PhoneVerificationChallengeStatus.Active)
            { await SaveAsync(records, current with { Status = PhoneVerificationChallengeStatus.Cancelled }); }
            return true;
        }, cancellationToken);

    public Task<PhoneVerificationChallengeRecord?> FindAsync(string verificationId, CancellationToken cancellationToken = default) =>
        database.WithRecordsAsync((records, _) => ReadAsync(records, verificationId), cancellationToken);

    public Task<PhoneVerificationAttemptResult> RecordRejectedAttemptAsync(string verificationId, int maximumAttempts,
        CancellationToken cancellationToken = default) => database.WithRecordsAsync(async (records, _) =>
        {
            if (maximumAttempts is < 1 or > 10) { throw new ArgumentOutOfRangeException(nameof(maximumAttempts)); }
            var current = await ReadAsync(records, verificationId);
            if (current is null) { return new PhoneVerificationAttemptResult(0, true); }
            if (current.Status == PhoneVerificationChallengeStatus.Locked) { return new PhoneVerificationAttemptResult(current.FailedAttempts, true); }
            if (current.Status != PhoneVerificationChallengeStatus.Active) { return new PhoneVerificationAttemptResult(current.FailedAttempts, true); }
            var attempts = checked(current.FailedAttempts + 1);
            var locked = attempts >= maximumAttempts;
            await SaveAsync(records, current with { FailedAttempts = attempts, Status = locked ? PhoneVerificationChallengeStatus.Locked : current.Status });
            if (locked) { await RemoveActiveAsync(records, current); }
            return new PhoneVerificationAttemptResult(attempts, locked);
        }, cancellationToken);

    public Task<bool> TryCompleteAsync(string verificationId, DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default) => database.WithRecordsAsync(async (records, _) =>
        {
            var current = await ReadAsync(records, verificationId);
            if (current is null || current.Status != PhoneVerificationChallengeStatus.Active || current.ExpiresAtUtc <= completedAtUtc) { return false; }
            var phone = await IdentityPersistenceContext.ReadAsync<PhoneState>(records, "phone_state", Owner, current.PhoneSubjectHash);
            if (phone?.ActiveId != verificationId) { return false; }
            await SaveAsync(records, current with { Status = PhoneVerificationChallengeStatus.Completed });
            await RemoveActiveAsync(records, current);
            return true;
        }, cancellationToken);

    private static async Task RemoveActiveAsync(OwnedRecordTransaction records, PhoneVerificationChallengeRecord challenge)
    {
        var phone = await IdentityPersistenceContext.ReadAsync<PhoneState>(records, "phone_state", Owner, challenge.PhoneSubjectHash);
        if (phone?.ActiveId == challenge.Id) { await IdentityPersistenceContext.WriteAsync(records, "phone_state", Owner, challenge.PhoneSubjectHash, phone with { ActiveId = null }); }
    }

    private static async Task<PhoneVerificationChallengeRecord?> ReadAsync(OwnedRecordTransaction records, string id)
    {
        var current = await IdentityPersistenceContext.ReadAsync<PhoneVerificationChallengeRecord>(records, "phone_challenges", Owner, id);
        if (current is not null)
        {
            Validate(current);
            if (current.Id != id) { throw new StorageUnavailableException(); }
        }
        return current;
    }

    private static Task SaveAsync(OwnedRecordTransaction records, PhoneVerificationChallengeRecord challenge)
    {
        Validate(challenge);
        return IdentityPersistenceContext.WriteAsync(records, "phone_challenges", Owner, challenge.Id, challenge);
    }

    private static void Validate(PhoneVerificationChallengeRecord challenge)
    {
        if (!Guid.TryParseExact(challenge.Id, "N", out _) || !ValidHash(challenge.PhoneSubjectHash) || !ValidHash(challenge.ClientInstanceHash) ||
            challenge.Purpose != "sign_in" || !Enum.IsDefined(challenge.Status) || challenge.FailedAttempts is < 0 or > 10 ||
            challenge.ExpiresAtUtc <= challenge.CreatedAtUtc || challenge.ExpiresAtUtc - challenge.CreatedAtUtc > TimeSpan.FromMinutes(30)
            || challenge.ResendAvailableAtUtc < challenge.CreatedAtUtc
            || challenge.ResendAvailableAtUtc - challenge.CreatedAtUtc > TimeSpan.FromSeconds(600)
            || challenge.ProviderReference is { Length: > 512 }
            || challenge.Status == PhoneVerificationChallengeStatus.Active && string.IsNullOrWhiteSpace(challenge.ProviderReference))
        {
            throw new StorageUnavailableException();
        }
    }
    private static bool ValidHash(string value) => IdentityRecordValidation.Hash(value);
    private static int Retry(TimeSpan delay) => Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds));
    internal sealed record PhoneState(string? ActiveId, DateTimeOffset[] Starts);
    internal sealed record ClientState(DateTimeOffset[] Starts);
}
