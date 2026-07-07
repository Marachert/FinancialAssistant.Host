using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Domain.Accounts;

namespace FinancialAssistant.Identity.Application.Providers.Google;

public sealed partial class GoogleProviderAuthenticationService
{
    private async Task<IdentityOperationResult<AuthSessionResponse>> CreateProviderAccountAsync(
        GoogleIdentityPrincipal principal,
        string subjectHash,
        GoogleSignInRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var account = IdentityAccount.Create(now);
        var tenantHash = string.IsNullOrWhiteSpace(principal.HostedDomain)
            ? null
            : identifierHasher.Hash(ProviderName, "tenant", principal.HostedDomain);
        var providerLink = new IdentityProviderLinkRecord(
            Guid.NewGuid().ToString("N"),
            account.Id,
            ProviderName,
            subjectHash,
            tenantHash,
            now,
            now);

        var created = await federatedAccountStore.TryCreateAccountWithProviderLinkAsync(
            account,
            providerLink,
            cancellationToken);
        if (!created)
        {
            var racedLink = await federatedAccountStore.FindProviderLinkAsync(
                ProviderName,
                subjectHash,
                cancellationToken);
            return racedLink is null
                ? ProviderUnavailable()
                : await AuthenticateLinkedAccountAsync(
                    racedLink,
                    subjectHash,
                    request,
                    correlationId,
                    cancellationToken);
        }

        var session = sessionIssuer.Issue(
            account,
            request.Client,
            AuthenticationMethod,
            now);
        await PublishRegistrationAsync(account.Id, correlationId, now, cancellationToken);
        await PublishSignInAsync(
            account.Id,
            session.User.SessionId,
            correlationId,
            now,
            cancellationToken);
        return IdentityOperationResult<AuthSessionResponse>.Success(session);
    }

    private async Task<IdentityOperationResult<AuthSessionResponse>> AuthenticateLinkedAccountAsync(
        IdentityProviderLinkRecord providerLink,
        string subjectHash,
        GoogleSignInRequest request,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var account = await accountStore.FindAccountByIdAsync(
            providerLink.AccountId,
            cancellationToken);
        if (account is null || !account.CanAuthenticate)
        {
            await PublishFailureAsync(correlationId, cancellationToken);
            return ProviderAuthenticationFailed();
        }

        var now = clock.UtcNow;
        await federatedAccountStore.TouchProviderLinkAsync(
            ProviderName,
            subjectHash,
            now,
            cancellationToken);
        var session = sessionIssuer.Issue(
            account,
            request.Client,
            AuthenticationMethod,
            now);
        await PublishSignInAsync(
            account.Id,
            session.User.SessionId,
            correlationId,
            now,
            cancellationToken);
        return IdentityOperationResult<AuthSessionResponse>.Success(session);
    }
}
