using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Domain.Accounts;

namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IIdentityAccountStore
{
    Task<EmailCredentialRecord?> FindCredentialByLookupHashAsync(string lookupKeyHash, CancellationToken cancellationToken = default);
    Task<IdentityAccount?> FindAccountByIdAsync(string accountId, CancellationToken cancellationToken = default);
    Task<bool> TryCreateAsync(IdentityAccount account, EmailCredentialRecord credential, CancellationToken cancellationToken = default);
    Task UpdateCredentialHashAsync(string lookupKeyHash, PasswordHashResult passwordHash, DateTimeOffset rotatedAtUtc, CancellationToken cancellationToken = default);
}
