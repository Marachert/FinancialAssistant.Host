using System.Security.Cryptography;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Sessions;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Identity.Infrastructure.Persistence;

public sealed class PostgresIdentitySessionStore : IIdentitySessionStore
{
    private const string Owner = "identity";
    private readonly IdentityPersistenceContext database;
    public PostgresIdentitySessionStore(IdentityPersistenceContext database) { this.database = database; }

    public Task<bool> TryCreateAsync(IdentitySessionRecord session, CancellationToken cancellationToken = default) =>
        database.WithRecordsAsync(async (records, _) =>
        {
            Validate(session);
            var family = HashKey(session.TokenFamilyIdHash);
            if (await records.GetAsync("session_locations", Owner, session.Id) is not null ||
                await records.GetAsync("family_revocations", Owner, family) is not null ||
                await records.GetAsync("accounts", Owner, session.AccountId) is null) { return false; }
            var account = await IdentityPersistenceContext.ReadAsync<PostgresIdentityAccountStore.AccountState>(records, "accounts", Owner, session.AccountId);
            if (account is null || account.Id != session.AccountId || account.Status != FinancialAssistant.Identity.Domain.Accounts.IdentityAccountStatus.Active) { return false; }
            var directory = await IdentityPersistenceContext.ReadAsync<FamilyState>(records, "families", Owner, family);
            if (directory is not null && directory.Family != family) { throw new StorageUnavailableException(); }
            if (directory is not null && directory.AccountId != session.AccountId) { return false; }
            await records.InsertAsync("session_locations", Owner, session.Id,
                IdentityPersistenceContext.Encode(new SessionLocation(session.Id, session.AccountId, family)));
            await records.InsertAsync("sessions", family, session.Id, IdentityPersistenceContext.Encode(session));
            await IdentityPersistenceContext.WriteAsync(records, "families", Owner, family,
                new FamilyState(family, session.AccountId, directory is not null && directory.ExpiresAtUtc > session.RefreshTokenExpiresAtUtc
                    ? directory.ExpiresAtUtc : session.RefreshTokenExpiresAtUtc));
            return true;
        }, cancellationToken);

    public Task<IdentitySessionRecord?> FindByIdAsync(string sessionId, CancellationToken cancellationToken = default) =>
        database.WithRecordsAsync((records, _) => ReadSessionAsync(records, sessionId), cancellationToken);

    public Task<SessionRotationStoreResult> RotateAsync(string sessionId, string presentedRefreshTokenHash,
        IdentitySessionRecord replacement, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        database.WithRecordsAsync(async (records, _) =>
        {
            var current = await ReadSessionAsync(records, sessionId);
            if (current is null) { return SessionRotationStoreResult.Missing; }
            if (!HashesMatch(current.RefreshTokenHash, presentedRefreshTokenHash)) { return SessionRotationStoreResult.InvalidSecret; }
            if (current.RefreshTokenExpiresAtUtc <= now || current.Status == IdentitySessionStatus.Expired)
            {
                await SaveSessionAsync(records, current with { Status = IdentitySessionStatus.Expired });
                return SessionRotationStoreResult.Expired;
            }
            if (current.Status == IdentitySessionStatus.Revoked) { return SessionRotationStoreResult.Revoked; }
            if (current.Status == IdentitySessionStatus.Rotated)
            {
                await RevokeFamilyAsync(records, current.TokenFamilyIdHash, now);
                return SessionRotationStoreResult.ReuseDetected;
            }
            Validate(replacement);
            if (replacement.AccountId != current.AccountId || !HashesMatch(replacement.TokenFamilyIdHash, current.TokenFamilyIdHash) ||
                replacement.Status != IdentitySessionStatus.Active || await records.GetAsync("session_locations", Owner, replacement.Id) is not null)
            {
                return SessionRotationStoreResult.InvalidSecret;
            }
            await SaveSessionAsync(records, current with { Status = IdentitySessionStatus.Rotated, RotatedAtUtc = now, ReplacedBySessionId = replacement.Id });
            await records.InsertAsync("session_locations", Owner, replacement.Id,
                IdentityPersistenceContext.Encode(new SessionLocation(replacement.Id, replacement.AccountId, HashKey(replacement.TokenFamilyIdHash))));
            await records.InsertAsync("sessions", HashKey(replacement.TokenFamilyIdHash), replacement.Id, IdentityPersistenceContext.Encode(replacement));
            var directory = await IdentityPersistenceContext.ReadAsync<FamilyState>(records, "families", Owner, HashKey(current.TokenFamilyIdHash));
            await IdentityPersistenceContext.WriteAsync(records, "families", Owner, HashKey(current.TokenFamilyIdHash),
                new FamilyState(HashKey(current.TokenFamilyIdHash), current.AccountId, directory is not null && directory.ExpiresAtUtc > replacement.RefreshTokenExpiresAtUtc
                    ? directory.ExpiresAtUtc : replacement.RefreshTokenExpiresAtUtc));
            return SessionRotationStoreResult.Success;
        }, cancellationToken);

    public Task<SessionRevocationStoreResult> RevokeAsync(string sessionId, string accountId, string presentedRefreshTokenHash,
        DateTimeOffset now, CancellationToken cancellationToken = default) => database.WithRecordsAsync(async (records, _) =>
        {
            var current = await ReadSessionAsync(records, sessionId);
            if (current is null) { return SessionRevocationStoreResult.Missing; }
            if (current.AccountId != accountId || !HashesMatch(current.RefreshTokenHash, presentedRefreshTokenHash)) { return SessionRevocationStoreResult.InvalidSecret; }
            if (current.RefreshTokenExpiresAtUtc <= now || current.Status == IdentitySessionStatus.Expired)
            {
                await SaveSessionAsync(records, current with { Status = IdentitySessionStatus.Expired });
                return SessionRevocationStoreResult.Expired;
            }
            if (current.Status == IdentitySessionStatus.Revoked) { return SessionRevocationStoreResult.Revoked; }
            if (current.Status == IdentitySessionStatus.Rotated) { await RevokeFamilyAsync(records, current.TokenFamilyIdHash, now); }
            else { await SaveSessionAsync(records, current with { Status = IdentitySessionStatus.Revoked, RevokedAtUtc = now }); }
            return SessionRevocationStoreResult.Success;
        }, cancellationToken);

    public Task RevokeFamilyAsync(string tokenFamilyIdHash, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        database.WithRecordsAsync(async (records, _) => { await RevokeFamilyAsync(records, tokenFamilyIdHash, now); return true; }, cancellationToken);

    private static async Task<IdentitySessionRecord?> ReadSessionAsync(OwnedRecordTransaction records, string id)
    {
        var location = await IdentityPersistenceContext.ReadAsync<SessionLocation>(records, "session_locations", Owner, id);
        if (location is null) { return null; }
        var current = await IdentityPersistenceContext.ReadAsync<IdentitySessionRecord>(records, "sessions", location.Family, id);
        if (current is null) { throw new StorageUnavailableException(); }
        Validate(current);
        if (location.Id != id || current.Id != id || current.AccountId != location.AccountId || HashKey(current.TokenFamilyIdHash) != location.Family) { throw new StorageUnavailableException(); }
        var revoked = await IdentityPersistenceContext.ReadAsync<FamilyRevocation>(records, "family_revocations", Owner, location.Family);
        return revoked is null ? current : current with { Status = IdentitySessionStatus.Revoked, RevokedAtUtc = revoked.AtUtc };
    }

    private static Task SaveSessionAsync(OwnedRecordTransaction records, IdentitySessionRecord session) =>
        IdentityPersistenceContext.WriteAsync(records, "sessions", HashKey(session.TokenFamilyIdHash), session.Id, session);

    private static async Task RevokeFamilyAsync(OwnedRecordTransaction records, string familyHash, DateTimeOffset now)
    {
        var family = HashKey(familyHash);
        if (await records.GetAsync("family_revocations", Owner, family) is null)
        {
            await records.InsertAsync("family_revocations", Owner, family, IdentityPersistenceContext.Encode(new FamilyRevocation(now)));
        }
    }

    private static string HashKey(string value)
    {
        if (value is null || value.Length != 64 || !value.All(char.IsAsciiHexDigit)) { throw new StorageUnavailableException(); }
        return Convert.ToHexString(Convert.FromHexString(value));
    }

    private static bool HashesMatch(string expected, string presented) =>
        expected is not null && presented is not null && expected.Length == 64 && presented.Length == 64 &&
        expected.All(char.IsAsciiHexDigit) && presented.All(char.IsAsciiHexDigit) &&
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(presented));

    private static void Validate(IdentitySessionRecord session)
    {
        if (!Guid.TryParseExact(session.Id, "N", out _) || !Guid.TryParseExact(session.AccountId, "N", out _) ||
            !Enum.IsDefined(session.Status) || string.IsNullOrWhiteSpace(session.AuthenticationMethod) || session.AuthenticationMethod.Length > 64 ||
            session.AccessTokenExpiresAtUtc < session.IssuedAtUtc || session.RefreshTokenExpiresAtUtc < session.IssuedAtUtc)
        {
            throw new StorageUnavailableException();
        }
        HashKey(session.TokenFamilyIdHash);
        HashKey(session.RefreshTokenHash);
        HashKey(session.ClientInstanceIdHash);
        IdentityRecordValidation.Require(session.AccessTokenExpiresAtUtc - session.IssuedAtUtc <= TimeSpan.FromHours(1)
            && session.RefreshTokenExpiresAtUtc - session.IssuedAtUtc <= TimeSpan.FromDays(90)
            && (session.ReplacedBySessionId is null || IdentityRecordValidation.Id(session.ReplacedBySessionId))
            && (session.Status != IdentitySessionStatus.Rotated || session.RotatedAtUtc is not null && session.ReplacedBySessionId is not null));
    }

    internal sealed record SessionLocation(string Id, string AccountId, string Family);
    internal sealed record FamilyState(string Family, string AccountId, DateTimeOffset ExpiresAtUtc);
    private sealed record FamilyRevocation(DateTimeOffset AtUtc);
}
