using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Application.Providers;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Domain.Accounts;

namespace FinancialAssistant.Identity.Application.Phone;

public sealed partial class PhoneVerificationAuthenticationService
{
    private async Task<IdentityOperationResult<AuthSessionResponse>> CompleteAuthenticationAsync(
        string phoneSubjectHash,
        IdentityClientContext client,
        string correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var link = await federatedAccountStore.FindProviderLinkAsync(
            ProviderName,
            phoneSubjectHash,
            cancellationToken);
        if (link is not null)
        {
            return await AuthenticateLinkedAccountAsync(
                link,
                phoneSubjectHash,
                client,
                correlationId,
                now,
                cancellationToken);
        }

        var account = IdentityAccount.Create(now);
        var newLink = new IdentityProviderLinkRecord(
            Guid.NewGuid().ToString("N"),
            account.Id,
            ProviderName,
            phoneSubjectHash,
            null,
            now,
            now);
        var created = await federatedAccountStore.TryCreateAccountWithProviderLinkAsync(
            account,
            newLink,
            cancellationToken);
        if (!created)
        {
            link = await federatedAccountStore.FindProviderLinkAsync(
                ProviderName,
                phoneSubjectHash,
                cancellationToken);
            if (link is null)
            {
                return ProviderUnavailableConfirm();
            }

            return await AuthenticateLinkedAccountAsync(
                link,
                phoneSubjectHash,
                client,
                correlationId,
                now,
                cancellationToken);
        }

        var session = sessionIssuer.Issue(account, client, AuthenticationMethod, now);
        await PublishRegistrationAsync(account.Id, correlationId, now, cancellationToken);
        await PublishSignInAsync(account.Id, session.User.SessionId, correlationId, now, cancellationToken);
        return IdentityOperationResult<AuthSessionResponse>.Success(session);
    }

    private async Task<IdentityOperationResult<AuthSessionResponse>> AuthenticateLinkedAccountAsync(
        IdentityProviderLinkRecord link,
        string phoneSubjectHash,
        IdentityClientContext client,
        string correlationId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var account = await accountStore.FindAccountByIdAsync(link.AccountId, cancellationToken);
        if (account is null || !account.CanAuthenticate)
        {
            await PublishFailureAsync(correlationId, cancellationToken);
            return VerificationFailed();
        }

        await federatedAccountStore.TouchProviderLinkAsync(
            ProviderName,
            phoneSubjectHash,
            now,
            cancellationToken);
        var session = sessionIssuer.Issue(account, client, AuthenticationMethod, now);
        await PublishSignInAsync(account.Id, session.User.SessionId, correlationId, now, cancellationToken);
        return IdentityOperationResult<AuthSessionResponse>.Success(session);
    }
}
