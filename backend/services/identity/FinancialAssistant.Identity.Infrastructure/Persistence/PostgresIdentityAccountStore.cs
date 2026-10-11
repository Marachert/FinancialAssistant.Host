using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Providers;
using FinancialAssistant.Identity.Domain.Accounts;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Identity.Infrastructure.Persistence;

public sealed class PostgresIdentityAccountStore : IIdentityAccountStore, IIdentityFederatedAccountStore
{
    private const string Owner = "identity";
    private readonly IdentityPersistenceContext database;
    public PostgresIdentityAccountStore(IdentityPersistenceContext database) { this.database = database; }

    public Task<EmailCredentialRecord?> FindCredentialByLookupHashAsync(string lookupKeyHash, CancellationToken cancellationToken = default) =>
        database.WithRecordsAsync(async (records, _) =>
        {
            IdentityRecordValidation.Require(IdentityRecordValidation.Hash(lookupKeyHash));
            var credential = await IdentityPersistenceContext.ReadAsync<EmailCredentialRecord>(records, "email_credentials", Owner, lookupKeyHash);
            if (credential is not null)
            {
                Validate(credential);
                IdentityRecordValidation.Require(credential.LookupKeyHash == lookupKeyHash);
            }
            return credential;
        }, cancellationToken);

    public Task<IdentityAccount?> FindAccountByIdAsync(string accountId, CancellationToken cancellationToken = default) =>
        database.WithRecordsAsync(async (records, _) =>
        {
            var account = await IdentityPersistenceContext.ReadAsync<AccountState>(records, "accounts", Owner, accountId);
            if (account is null) { return null; }
            if (account.Id != accountId) { throw new StorageUnavailableException(); }
            try { return IdentityAccount.Rehydrate(account.Id, account.Status, account.Roles, account.CreatedAtUtc); }
            catch (ArgumentException) { throw new StorageUnavailableException(); }
        }, cancellationToken);

    public Task<bool> TryCreateAsync(IdentityAccount account, EmailCredentialRecord credential, CancellationToken cancellationToken = default) =>
        database.WithRecordsAsync(async (records, _) =>
        {
            Validate(credential);
            if (credential.AccountId != account.Id) { throw new ArgumentException("Identity credential owner is invalid."); }
            if (await records.GetAsync("accounts", Owner, account.Id) is not null ||
                await records.GetAsync("email_credentials", Owner, credential.LookupKeyHash) is not null) { return false; }
            await records.InsertAsync("accounts", Owner, account.Id, IdentityPersistenceContext.Encode(State(account)));
            await records.InsertAsync("email_credentials", Owner, credential.LookupKeyHash, IdentityPersistenceContext.Encode(credential));
            return true;
        }, cancellationToken);

    public Task UpdateCredentialHashAsync(string lookupKeyHash, PasswordHashResult passwordHash, DateTimeOffset rotatedAtUtc,
        CancellationToken cancellationToken = default) => database.WithRecordsAsync(async (records, _) =>
        {
            var current = await IdentityPersistenceContext.ReadAsync<EmailCredentialRecord>(records, "email_credentials", Owner, lookupKeyHash);
            if (current is not null)
            {
                Validate(current);
                IdentityRecordValidation.Require(current.LookupKeyHash == lookupKeyHash);
                var updated = current with
                {
                    SecretHash = passwordHash.Hash,
                    SecretHashAlgorithm = passwordHash.Algorithm,
                    SecretHashParameters = passwordHash.Parameters,
                    LastRotatedAtUtc = rotatedAtUtc
                };
                Validate(updated);
                await IdentityPersistenceContext.WriteAsync(records, "email_credentials", Owner, lookupKeyHash, updated);
            }
            return true;
        }, cancellationToken);

    public Task<IdentityProviderLinkRecord?> FindProviderLinkAsync(string provider, string providerSubjectHash,
        CancellationToken cancellationToken = default) => database.WithRecordsAsync(async (records, _) =>
        {
            var key = ProviderKey(provider, providerSubjectHash);
            var link = await IdentityPersistenceContext.ReadAsync<IdentityProviderLinkRecord>(records, "provider_links", Owner, key);
            if (link is not null) { Validate(link); IdentityRecordValidation.Require(ProviderKey(link.Provider, link.ProviderSubjectHash) == key); }
            return link;
        }, cancellationToken);

    public Task<bool> TryCreateAccountWithProviderLinkAsync(IdentityAccount account, IdentityProviderLinkRecord providerLink,
        CancellationToken cancellationToken = default) => database.WithRecordsAsync(async (records, _) =>
        {
            Validate(providerLink);
            if (providerLink.AccountId != account.Id) { throw new ArgumentException("Identity provider-link owner is invalid."); }
            var key = ProviderKey(providerLink.Provider, providerLink.ProviderSubjectHash);
            if (await records.GetAsync("accounts", Owner, account.Id) is not null || await records.GetAsync("provider_links", Owner, key) is not null) { return false; }
            await records.InsertAsync("accounts", Owner, account.Id, IdentityPersistenceContext.Encode(State(account)));
            await records.InsertAsync("provider_links", Owner, key, IdentityPersistenceContext.Encode(providerLink));
            return true;
        }, cancellationToken);

    public Task TouchProviderLinkAsync(string provider, string providerSubjectHash, DateTimeOffset authenticatedAtUtc,
        CancellationToken cancellationToken = default) => database.WithRecordsAsync(async (records, _) =>
        {
            var key = ProviderKey(provider, providerSubjectHash);
            var current = await IdentityPersistenceContext.ReadAsync<IdentityProviderLinkRecord>(records, "provider_links", Owner, key);
            if (current is not null)
            {
                Validate(current);
                IdentityRecordValidation.Require(ProviderKey(current.Provider, current.ProviderSubjectHash) == key);
                await IdentityPersistenceContext.WriteAsync(records, "provider_links", Owner, key, current with { LastAuthenticatedAtUtc = authenticatedAtUtc });
            }
            return true;
        }, cancellationToken);

    private static string ProviderKey(string provider, string subject)
    {
        IdentityRecordValidation.Require(provider is "google" or "apple" or "phone" && IdentityRecordValidation.Hash(subject));
        return $"{provider.Length}:{provider}{subject}";
    }
    private static void Validate(EmailCredentialRecord credential) => IdentityRecordValidation.Require(
        credential is not null && IdentityRecordValidation.Id(credential.Id) && IdentityRecordValidation.Id(credential.AccountId)
        && IdentityRecordValidation.Hash(credential.LookupKeyHash) && !string.IsNullOrWhiteSpace(credential.SecretHash)
        && credential.SecretHash.Length <= 4096 && !string.IsNullOrWhiteSpace(credential.SecretHashAlgorithm)
        && credential.SecretHashAlgorithm.Length <= 128 && credential.SecretHashParameters is { Length: <= 512 });
    private static void Validate(IdentityProviderLinkRecord link) => IdentityRecordValidation.Require(
        link is not null && IdentityRecordValidation.Id(link.Id) && IdentityRecordValidation.Id(link.AccountId)
        && link.Provider is "google" or "apple" or "phone" && IdentityRecordValidation.Hash(link.ProviderSubjectHash)
        && (link.ProviderTenantHash is null || IdentityRecordValidation.Hash(link.ProviderTenantHash)));
    private static AccountState State(IdentityAccount account) => new(account.Id, account.Status, account.Roles.ToArray(), account.CreatedAtUtc);
    internal sealed record AccountState(string Id, IdentityAccountStatus Status, string[] Roles, DateTimeOffset CreatedAtUtc);
}
