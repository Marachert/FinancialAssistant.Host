using FinancialAssistant.Identity.Application.Providers;
using FinancialAssistant.Identity.Domain.Accounts;

namespace FinancialAssistant.Identity.Application.Abstractions;

public interface IIdentityFederatedAccountStore
{
    Task<IdentityProviderLinkRecord?> FindProviderLinkAsync(
        string provider,
        string providerSubjectHash,
        CancellationToken cancellationToken = default);

    Task<bool> TryCreateAccountWithProviderLinkAsync(
        IdentityAccount account,
        IdentityProviderLinkRecord providerLink,
        CancellationToken cancellationToken = default);

    Task TouchProviderLinkAsync(
        string provider,
        string providerSubjectHash,
        DateTimeOffset authenticatedAtUtc,
        CancellationToken cancellationToken = default);
}
