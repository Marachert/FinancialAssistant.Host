using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Messaging;
using FinancialAssistant.Identity.Application.Phone;
using FinancialAssistant.Identity.Application.Providers;
using FinancialAssistant.Identity.Application.Sessions;
using FinancialAssistant.Identity.Domain.Accounts;
using FinancialAssistant.Identity.Infrastructure.Configuration;
using FinancialAssistant.Shared.Persistence;
using Microsoft.Extensions.Options;

namespace FinancialAssistant.Identity.Infrastructure.Persistence;

// Installer/operator integration must authorize calls; this is not a public HTTP deletion endpoint.
public sealed class IdentityPersistenceMaintenance(IdentityPersistenceContext context, IOptions<IdentityServiceOptions> options)
{
    private const string Owner = "identity";
    private static readonly string[] CleanupBuckets = ["session_locations", "families", "phone_challenges", "phone_state", "phone_clients", "outbox", "deletion_jobs"];
    private static readonly string[] DeletionBuckets = ["email_credentials", "provider_links", "session_locations"];

    public Task<bool> ProcessDeletionBatchAsync(DateTimeOffset now, CancellationToken token = default) =>
        context.WithRecordsAsync(async (records, _) =>
        {
            var scan = await IdentityPersistenceContext.ReadAsync<DeletionScan>(records, "cleanup_scan", Owner, "deletions");
            var page = await records.ReadPageAsync("deletion_jobs", Owner, scan?.After, 1);
            await IdentityPersistenceContext.WriteAsync(records, "cleanup_scan", Owner, "deletions", new DeletionScan(page.LastOrDefault()?.Cursor));
            if (page.Count == 0) { return false; }
            var job = IdentityPersistenceContext.Decode<DeletionJob>(new DurableRecord(page[0].Revision, page[0].Payload));
            Validate(job);
            return job.CompletedAtUtc is not null || await DeleteAccountBatchAsync(job.AccountId, now, token);
        }, token);

    public Task<bool> DeleteAccountBatchAsync(string accountId, DateTimeOffset now, CancellationToken token = default) =>
        context.WithRecordsAsync(async (records, _) =>
        {
            IdentityRecordValidation.Require(IdentityRecordValidation.Id(accountId));
            var account = await IdentityPersistenceContext.ReadAsync<PostgresIdentityAccountStore.AccountState>(records, "accounts", Owner, accountId);
            if (account is null) { return true; }
            IdentityRecordValidation.Require(account.Id == accountId);
            IdentityAccount.Rehydrate(account.Id, account.Status, account.Roles, account.CreatedAtUtc);
            var job = await IdentityPersistenceContext.ReadAsync<DeletionJob>(records, "deletion_jobs", Owner, accountId);
            if (job is null)
            {
                job = new DeletionJob(accountId, now, 0, null, null);
                await IdentityPersistenceContext.WriteAsync(records, "deletion_jobs", Owner, accountId, job);
                await IdentityPersistenceContext.WriteAsync(records, "accounts", Owner, accountId, account with { Status = IdentityAccountStatus.DeletionPending });
            }
            Validate(job);
            IdentityRecordValidation.Require(job.AccountId == accountId);
            if (job.CompletedAtUtc is not null) { return true; }
            var retention = options.Value.Storage.Cleanup;
            if (now < job.RequestedAtUtc.AddDays(Math.Max(retention.RemovedCredentialRetentionDays, retention.RemovedProviderLinkRetentionDays)))
            { return false; }
            var bucket = DeletionBuckets[job.Phase];
            var page = await records.ReadPageAsync(bucket, Owner, job.After, 25);
            foreach (var item in page)
            {
                var value = new DurableRecord(item.Revision, item.Payload);
                var owned = bucket switch
                {
                    "email_credentials" => IdentityPersistenceContext.Decode<EmailCredentialRecord>(value).AccountId,
                    "provider_links" => IdentityPersistenceContext.Decode<IdentityProviderLinkRecord>(value).AccountId,
                    _ => IdentityPersistenceContext.Decode<PostgresIdentitySessionStore.SessionLocation>(value).AccountId
                };
                if (owned != accountId) { continue; }
                if (bucket == "session_locations")
                { await DeleteSessionAsync(records, IdentityPersistenceContext.Decode<PostgresIdentitySessionStore.SessionLocation>(value)); }
                await records.DeletePageItemAsync(bucket, Owner, item.Cursor, item.Revision);
            }
            job = job with { After = page.LastOrDefault()?.Cursor };
            if (page.Count < 25)
            {
                job = job with { Phase = job.Phase + 1, After = null };
                if (job.Phase == DeletionBuckets.Length)
                {
                    job = job with { CompletedAtUtc = now };
                    await IdentityPersistenceContext.WriteAsync(records, "accounts", Owner, accountId, account with { Status = IdentityAccountStatus.Deleted });
                }
            }
            await IdentityPersistenceContext.WriteAsync(records, "deletion_jobs", Owner, accountId, job);
            return job.CompletedAtUtc is not null;
        }, token);

    public Task<int> CleanupBatchAsync(DateTimeOffset now, CancellationToken token = default) =>
        context.WithRecordsAsync(async (records, _) =>
        {
            var scan = await IdentityPersistenceContext.ReadAsync<CleanupScan>(records, "cleanup_scan", Owner, "retention") ?? new CleanupScan(0, null);
            IdentityRecordValidation.Require(scan.Bucket >= 0 && scan.Bucket < CleanupBuckets.Length);
            var bucket = CleanupBuckets[scan.Bucket];
            var page = await records.ReadPageAsync(bucket, Owner, scan.After, 25);
            var removed = 0;
            foreach (var item in page)
            {
                var value = new DurableRecord(item.Revision, item.Payload);
                var remove = await CanRemoveAsync(records, bucket, value, now);
                if (remove) { await records.DeletePageItemAsync(bucket, Owner, item.Cursor, item.Revision); removed++; }
            }
            scan = page.Count < 25 ? new CleanupScan((scan.Bucket + 1) % CleanupBuckets.Length, null)
                : scan with { After = page[^1].Cursor };
            await IdentityPersistenceContext.WriteAsync(records, "cleanup_scan", Owner, "retention", scan);
            return removed;
        }, token);

    private async Task<bool> CanRemoveAsync(OwnedRecordTransaction records, string bucket, DurableRecord value, DateTimeOffset now)
    {
        var retention = options.Value.Storage.Cleanup;
        switch (bucket)
        {
            case "session_locations":
                var location = IdentityPersistenceContext.Decode<PostgresIdentitySessionStore.SessionLocation>(value);
                var session = await IdentityPersistenceContext.ReadAsync<IdentitySessionRecord>(records, "sessions", location.Family, location.Id)
                    ?? throw new StorageUnavailableException();
                IdentityRecordValidation.Require(session.Id == location.Id && session.AccountId == location.AccountId);
                // Retain old credentials until their whole validity window plus replay retention has ended.
                if (now < session.RefreshTokenExpiresAtUtc.AddDays(retention.TerminalSessionRetentionDays)) { return false; }
                await DeleteSessionAsync(records, location);
                return true;
            case "families":
                var family = IdentityPersistenceContext.Decode<PostgresIdentitySessionStore.FamilyState>(value);
                if (now < family.ExpiresAtUtc.AddDays(retention.TerminalSessionRetentionDays)
                    || (await records.ReadPageAsync("sessions", family.Family, pageSize: 1)).Count != 0) { return false; }
                var revoked = await records.GetAsync("family_revocations", Owner, family.Family);
                if (revoked is not null) { await records.DeleteAsync("family_revocations", Owner, family.Family, revoked.Revision); }
                return true;
            case "phone_challenges":
                var challenge = IdentityPersistenceContext.Decode<PhoneVerificationChallengeRecord>(value);
                if (now < challenge.ExpiresAtUtc.AddDays(retention.TerminalSessionRetentionDays)) { return false; }
                var phoneState = await IdentityPersistenceContext.ReadAsync<PostgresPhoneVerificationChallengeStore.PhoneState>(records, "phone_state", Owner, challenge.PhoneSubjectHash);
                if (phoneState?.ActiveId == challenge.Id)
                { await IdentityPersistenceContext.WriteAsync(records, "phone_state", Owner, challenge.PhoneSubjectHash, phoneState with { ActiveId = null }); }
                return true;
            case "phone_state":
                var phone = IdentityPersistenceContext.Decode<PostgresPhoneVerificationChallengeStore.PhoneState>(value);
                IdentityRecordValidation.Require(phone.Starts is { Length: <= 100 });
                var active = phone.ActiveId is null ? null : await IdentityPersistenceContext.ReadAsync<PhoneVerificationChallengeRecord>(records, "phone_challenges", Owner, phone.ActiveId);
                return phone.Starts.All(time => time <= now.AddDays(-1)) && (active is null || active.ExpiresAtUtc <= now);
            case "phone_clients":
                var client = IdentityPersistenceContext.Decode<PostgresPhoneVerificationChallengeStore.ClientState>(value);
                IdentityRecordValidation.Require(client.Starts is { Length: <= 500 });
                return client.Starts.All(time => time <= now.AddDays(-1));
            case "outbox":
                var message = IdentityPersistenceContext.Decode<IdentityOutboxMessage>(value);
                return message.PublishedAtUtc is not null && now >= message.PublishedAtUtc.Value.AddDays(30);
            case "deletion_jobs":
                var job = IdentityPersistenceContext.Decode<DeletionJob>(value);
                Validate(job);
                if (job.CompletedAtUtc is null || now < job.CompletedAtUtc.Value.AddDays(retention.DeletedAccountRetentionDays)) { return false; }
                var account = await records.GetAsync("accounts", Owner, job.AccountId);
                if (account is not null)
                {
                    var state = IdentityPersistenceContext.Decode<PostgresIdentityAccountStore.AccountState>(account);
                    IdentityRecordValidation.Require(state.Id == job.AccountId && state.Status == IdentityAccountStatus.Deleted);
                    await records.DeleteAsync("accounts", Owner, job.AccountId, account.Revision);
                }
                return true;
            default: throw new StorageUnavailableException();
        }
    }

    private static async Task DeleteSessionAsync(OwnedRecordTransaction records, PostgresIdentitySessionStore.SessionLocation location)
    {
        IdentityRecordValidation.Require(IdentityRecordValidation.Id(location.Id) && IdentityRecordValidation.Id(location.AccountId)
            && location.Family is { Length: 64 } && location.Family.All(char.IsAsciiHexDigit));
        var session = await records.GetAsync("sessions", location.Family, location.Id) ?? throw new StorageUnavailableException();
        var state = IdentityPersistenceContext.Decode<IdentitySessionRecord>(session);
        IdentityRecordValidation.Require(state.Id == location.Id && state.AccountId == location.AccountId
            && state.TokenFamilyIdHash is { Length: 64 } && state.TokenFamilyIdHash.All(char.IsAsciiHexDigit)
            && Convert.ToHexString(Convert.FromHexString(state.TokenFamilyIdHash)) == location.Family);
        await records.DeleteAsync("sessions", location.Family, location.Id, session.Revision);
    }
    private static void Validate(DeletionJob job) => IdentityRecordValidation.Require(IdentityRecordValidation.Id(job.AccountId)
        && job.Phase >= 0 && job.Phase <= DeletionBuckets.Length && (job.Phase < DeletionBuckets.Length || job.CompletedAtUtc is not null));
    private sealed record DeletionJob(string AccountId, DateTimeOffset RequestedAtUtc, int Phase, string? After, DateTimeOffset? CompletedAtUtc);
    private sealed record CleanupScan(int Bucket, string? After);
    private sealed record DeletionScan(string? After);
}
