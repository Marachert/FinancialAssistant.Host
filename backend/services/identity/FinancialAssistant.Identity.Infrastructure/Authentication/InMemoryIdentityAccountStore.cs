using System.Collections.Concurrent;
using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Providers;
using FinancialAssistant.Identity.Domain.Accounts;

namespace FinancialAssistant.Identity.Infrastructure.Authentication;

public sealed class InMemoryIdentityAccountStore : IIdentityAccountStore, IIdentityFederatedAccountStore
{
    private readonly object providerAccountGate = new();
    private readonly ConcurrentDictionary<string, IdentityAccount> accounts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, EmailCredentialRecord> credentials = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IdentityProviderLinkRecord> providerLinks = new(StringComparer.Ordinal);

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

    public Task<IdentityProviderLinkRecord?> FindProviderLinkAsync(
        string provider,
        string providerSubjectHash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        providerLinks.TryGetValue(CreateProviderKey(provider, providerSubjectHash), out var providerLink);
        return Task.FromResult(providerLink);
    }

    public Task<bool> TryCreateAccountWithProviderLinkAsync(
        IdentityAccount account,
        IdentityProviderLinkRecord providerLink,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var providerKey = CreateProviderKey(providerLink.Provider, providerLink.ProviderSubjectHash);

        lock (providerAccountGate)
        {
            if (providerLinks.ContainsKey(providerKey) || accounts.ContainsKey(account.Id))
            {
                return Task.FromResult(false);
            }

            if (!accounts.TryAdd(account.Id, account))
            {
                return Task.FromResult(false);
            }

            if (providerLinks.TryAdd(providerKey, providerLink))
            {
                return Task.FromResult(true);
            }

            accounts.TryRemove(account.Id, out _);
            return Task.FromResult(false);
        }
    }

    public Task TouchProviderLinkAsync(
        string provider,
        string providerSubjectHash,
        DateTimeOffset authenticatedAtUtc,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var providerKey = CreateProviderKey(provider, providerSubjectHash);

        while (providerLinks.TryGetValue(providerKey, out var current))
        {
            var replacement = current with { LastAuthenticatedAtUtc = authenticatedAtUtc };
            if (providerLinks.TryUpdate(providerKey, replacement, current))
            {
                break;
            }
        }

        return Task.CompletedTask;
    }

    private static string CreateProviderKey(string provider, string providerSubjectHash) =>
        $"{provider.Trim().ToLowerInvariant()}:{providerSubjectHash}";
}
