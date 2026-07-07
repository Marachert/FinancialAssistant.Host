using System.Collections.Concurrent;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Domain.Accounts;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class InMemoryIdentityAccountStore : IIdentityAccountStore
{
    private readonly ConcurrentDictionary<string, IdentityAccount> accounts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, EmailCredentialRecord> credentials = new(StringComparer.Ordinal);

    public Task<EmailCredentialRecord?> FindCredentialByLookupHashAsync(
        string lookupKeyHash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        credentials.TryGetValue(lookupKeyHash, out var credential);
        return Task.FromResult(credential);
    }

    public Task<IdentityAccount?> FindAccountByIdAsync(
        string accountId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        accounts.TryGetValue(accountId, out var account);
        return Task.FromResult(account);
    }

    public Task<bool> TryCreateAsync(
        IdentityAccount account,
        EmailCredentialRecord credential,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!credentials.TryAdd(credential.LookupKeyHash, credential))
        {
            return Task.FromResult(false);
        }

        if (accounts.TryAdd(account.Id, account))
        {
            return Task.FromResult(true);
        }

        credentials.TryRemove(credential.LookupKeyHash, out _);
        return Task.FromResult(false);
    }

    public Task UpdateCredentialHashAsync(
        string lookupKeyHash,
        PasswordHashResult passwordHash,
        DateTimeOffset rotatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        while (credentials.TryGetValue(lookupKeyHash, out var current))
        {
            var replacement = current with
            {
                SecretHash = passwordHash.Hash,
                SecretHashAlgorithm = passwordHash.Algorithm,
                SecretHashParameters = passwordHash.Parameters,
                LastRotatedAtUtc = rotatedAtUtc
            };

            if (credentials.TryUpdate(lookupKeyHash, replacement, current))
            {
                break;
            }
        }

        return Task.CompletedTask;
    }
}
