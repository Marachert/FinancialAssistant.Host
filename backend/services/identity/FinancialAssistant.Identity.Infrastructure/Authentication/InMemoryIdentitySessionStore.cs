using System.Security.Cryptography;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Sessions;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class InMemoryIdentitySessionStore : IIdentitySessionStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, IdentitySessionRecord> sessions = new(StringComparer.Ordinal);

    public Task<bool> TryCreateAsync(
        IdentitySessionRecord session,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(sessions.TryAdd(session.Id, session));
        }
    }

    public Task<IdentitySessionRecord?> FindByIdAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            sessions.TryGetValue(sessionId, out var session);
            return Task.FromResult(session);
        }
    }

    public Task<SessionRotationStoreResult> RotateAsync(
        string sessionId,
        string presentedRefreshTokenHash,
        IdentitySessionRecord replacement,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!sessions.TryGetValue(sessionId, out var current))
            {
                return Task.FromResult(SessionRotationStoreResult.Missing);
            }

            if (!HashesMatch(current.RefreshTokenHash, presentedRefreshTokenHash))
            {
                return Task.FromResult(SessionRotationStoreResult.InvalidSecret);
            }

            if (current.RefreshTokenExpiresAtUtc <= now || current.Status == IdentitySessionStatus.Expired)
            {
                sessions[sessionId] = current with { Status = IdentitySessionStatus.Expired };
                return Task.FromResult(SessionRotationStoreResult.Expired);
            }

            if (current.Status == IdentitySessionStatus.Revoked)
            {
                return Task.FromResult(SessionRotationStoreResult.Revoked);
            }

            if (current.Status == IdentitySessionStatus.Rotated)
            {
                RevokeFamily(current.TokenFamilyIdHash, now);
                return Task.FromResult(SessionRotationStoreResult.ReuseDetected);
            }

            if (sessions.ContainsKey(replacement.Id))
            {
                return Task.FromResult(SessionRotationStoreResult.InvalidSecret);
            }

            sessions[sessionId] = current with
            {
                Status = IdentitySessionStatus.Rotated,
                RotatedAtUtc = now,
                ReplacedBySessionId = replacement.Id
            };
            sessions.Add(replacement.Id, replacement);
            return Task.FromResult(SessionRotationStoreResult.Success);
        }
    }

    public Task<SessionRevocationStoreResult> RevokeAsync(
        string sessionId,
        string accountId,
        string presentedRefreshTokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!sessions.TryGetValue(sessionId, out var current))
            {
                return Task.FromResult(SessionRevocationStoreResult.Missing);
            }

            if (!string.Equals(current.AccountId, accountId, StringComparison.Ordinal)
                || !HashesMatch(current.RefreshTokenHash, presentedRefreshTokenHash))
            {
                return Task.FromResult(SessionRevocationStoreResult.InvalidSecret);
            }

            if (current.RefreshTokenExpiresAtUtc <= now || current.Status == IdentitySessionStatus.Expired)
            {
                sessions[sessionId] = current with { Status = IdentitySessionStatus.Expired };
                return Task.FromResult(SessionRevocationStoreResult.Expired);
            }

            if (current.Status == IdentitySessionStatus.Revoked)
            {
                return Task.FromResult(SessionRevocationStoreResult.Revoked);
            }

            if (current.Status == IdentitySessionStatus.Rotated)
            {
                RevokeFamily(current.TokenFamilyIdHash, now);
                return Task.FromResult(SessionRevocationStoreResult.Success);
            }

            sessions[sessionId] = current with
            {
                Status = IdentitySessionStatus.Revoked,
                RevokedAtUtc = now
            };
            return Task.FromResult(SessionRevocationStoreResult.Success);
        }
    }

    public Task RevokeFamilyAsync(
        string tokenFamilyIdHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            RevokeFamily(tokenFamilyIdHash, now);
            return Task.CompletedTask;
        }
    }

    private void RevokeFamily(string familyHash, DateTimeOffset now)
    {
        foreach (var pair in sessions
                     .Where(pair => string.Equals(pair.Value.TokenFamilyIdHash, familyHash, StringComparison.Ordinal))
                     .ToArray())
        {
            sessions[pair.Key] = pair.Value with
            {
                Status = IdentitySessionStatus.Revoked,
                RevokedAtUtc = now
            };
        }
    }

    private static bool HashesMatch(string expected, string presented)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(expected),
                Convert.FromHexString(presented));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
