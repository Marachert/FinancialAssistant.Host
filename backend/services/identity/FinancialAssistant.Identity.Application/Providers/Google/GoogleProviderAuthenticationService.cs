using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Domain.Accounts;

namespace FinancialAssistant.Identity.Application.Providers.Google;

public sealed partial class GoogleProviderAuthenticationService : IGoogleProviderAuthenticationService
{
    private const string ProviderName = "google";
    private const string AuthenticationMethod = "google_oidc";
    private readonly IGoogleIdentityTokenValidator tokenValidator;
    private readonly IIdentityProviderIdentifierHasher identifierHasher;
    private readonly IIdentityFederatedAccountStore federatedAccountStore;
    private readonly IIdentityAccountStore accountStore;
    private readonly IEmailLookupHasher emailLookupHasher;
    private readonly IInitialSessionIssuer sessionIssuer;
    private readonly IIdentityEventPublisher eventPublisher;
    private readonly ISystemClock clock;

    public GoogleProviderAuthenticationService(
        IGoogleIdentityTokenValidator tokenValidator,
        IIdentityProviderIdentifierHasher identifierHasher,
        IIdentityFederatedAccountStore federatedAccountStore,
        IIdentityAccountStore accountStore,
        IEmailLookupHasher emailLookupHasher,
        IInitialSessionIssuer sessionIssuer,
        IIdentityEventPublisher eventPublisher,
        ISystemClock clock)
    {
        this.tokenValidator = tokenValidator;
        this.identifierHasher = identifierHasher;
        this.federatedAccountStore = federatedAccountStore;
        this.accountStore = accountStore;
        this.emailLookupHasher = emailLookupHasher;
        this.sessionIssuer = sessionIssuer;
        this.eventPublisher = eventPublisher;
        this.clock = clock;
    }

    public async Task<IdentityOperationResult<AuthSessionResponse>> AuthenticateAsync(
        GoogleSignInRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = IdentityRequestValidator.ValidateGoogleSignIn(request);
        if (validationErrors.Count > 0)
        {
            return InvalidRequest(validationErrors);
        }

        var validation = await tokenValidator.ValidateAsync(request.IdToken, cancellationToken);
        if (validation.Status == GoogleIdentityValidationStatus.Unavailable)
        {
            return ProviderUnavailable();
        }

        if (validation.Status != GoogleIdentityValidationStatus.Valid
            || validation.Principal is null
            || string.IsNullOrWhiteSpace(validation.Principal.Subject))
        {
            await PublishFailureAsync(correlationId, cancellationToken);
            return ProviderAuthenticationFailed();
        }

        var principal = validation.Principal;
        var subjectHash = identifierHasher.Hash(ProviderName, "subject", principal.Subject);
        var existingLink = await federatedAccountStore.FindProviderLinkAsync(
            ProviderName,
            subjectHash,
            cancellationToken);
        if (existingLink is not null)
        {
            return await AuthenticateLinkedAccountAsync(
                existingLink,
                subjectHash,
                request,
                correlationId,
                cancellationToken);
        }

        if (await RequiresExplicitLinkAsync(principal, cancellationToken))
        {
            return ProviderLinkRequired();
        }

        return await CreateProviderAccountAsync(
            principal,
            subjectHash,
            request,
            correlationId,
            cancellationToken);
    }

    private async Task<bool> RequiresExplicitLinkAsync(
        GoogleIdentityPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (!principal.EmailVerified || string.IsNullOrWhiteSpace(principal.Email))
        {
            return false;
        }

        var lookupHash = emailLookupHasher.Hash(
            EmailIdentityNormalizer.Normalize(principal.Email));
        var localCredential = await accountStore.FindCredentialByLookupHashAsync(
            lookupHash,
            cancellationToken);
        return localCredential is not null;
    }
}
