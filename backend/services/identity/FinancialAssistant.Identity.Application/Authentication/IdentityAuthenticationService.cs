using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Messaging;
using FinancialAssistant.Identity.Contracts.Auth;
using FinancialAssistant.Identity.Domain.Accounts;

namespace FinancialAssistant.Identity.Application.Authentication;

public sealed class IdentityAuthenticationService : IIdentityAuthenticationService
{
    private readonly IIdentityAccountStore accountStore;
    private readonly IEmailLookupHasher emailLookupHasher;
    private readonly IPasswordCredentialHasher passwordHasher;
    private readonly IInitialSessionIssuer sessionIssuer;
    private readonly IIdentityEventPublisher eventPublisher;
    private readonly ISystemClock clock;

    public IdentityAuthenticationService(
        IIdentityAccountStore accountStore,
        IEmailLookupHasher emailLookupHasher,
        IPasswordCredentialHasher passwordHasher,
        IInitialSessionIssuer sessionIssuer,
        IIdentityEventPublisher eventPublisher,
        ISystemClock clock)
    {
        this.accountStore = accountStore;
        this.emailLookupHasher = emailLookupHasher;
        this.passwordHasher = passwordHasher;
        this.sessionIssuer = sessionIssuer;
        this.eventPublisher = eventPublisher;
        this.clock = clock;
    }

    public async Task<IdentityOperationResult<AuthSessionResponse>> RegisterAsync(
        RegisterAccountRequest request,
        string? idempotencyKey,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = IdentityRequestValidator.ValidateRegistration(request, idempotencyKey);
        if (validationErrors.Count > 0)
        {
            return IdentityOperationResult<AuthSessionResponse>.Failed(
                IdentityFailureKind.Validation,
                IdentityErrorCodes.ValidationFailed,
                "Registration request is invalid.",
                "Correct the highlighted fields and submit the request again.",
                validationErrors);
        }

        var normalizedEmail = EmailIdentityNormalizer.Normalize(request.Email);
        var lookupKeyHash = emailLookupHasher.Hash(normalizedEmail);
        var existingCredential = await accountStore.FindCredentialByLookupHashAsync(lookupKeyHash, cancellationToken);
        if (existingCredential is not null)
        {
            return DuplicateRegistration();
        }

        var now = clock.UtcNow;
        var account = IdentityAccount.Create(now);
        var passwordHash = passwordHasher.Hash(account.Id, request.Password);
        var credential = new EmailCredentialRecord(
            Guid.NewGuid().ToString("N"),
            account.Id,
            lookupKeyHash,
            passwordHash.Hash,
            passwordHash.Algorithm,
            passwordHash.Parameters,
            now);

        var created = await accountStore.TryCreateAsync(account, credential, cancellationToken);
        if (!created)
        {
            return DuplicateRegistration();
        }

        var session = sessionIssuer.Issue(account, request.Client, now);
        await eventPublisher.PublishAsync(
            new IdentityEventPublication(
                "user.registered.v1",
                "1",
                now,
                correlationId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["userId"] = account.Id,
                    ["authenticationMethod"] = "email_password"
                }),
            cancellationToken);

        return IdentityOperationResult<AuthSessionResponse>.Success(session);
    }

    public async Task<IdentityOperationResult<AuthSessionResponse>> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = IdentityRequestValidator.ValidateSignIn(request);
        if (validationErrors.Count > 0)
        {
            return IdentityOperationResult<AuthSessionResponse>.Failed(
                IdentityFailureKind.Validation,
                IdentityErrorCodes.ValidationFailed,
                "Sign-in request is invalid.",
                "Correct the highlighted fields and submit the request again.",
                validationErrors);
        }

        var normalizedEmail = EmailIdentityNormalizer.Normalize(request.Email);
        var lookupKeyHash = emailLookupHasher.Hash(normalizedEmail);
        var credential = await accountStore.FindCredentialByLookupHashAsync(lookupKeyHash, cancellationToken);

        if (credential is null)
        {
            passwordHasher.VerifyDummy(request.Password);
            return AuthenticationFailed();
        }

        var account = await accountStore.FindAccountByIdAsync(credential.AccountId, cancellationToken);
        if (account is null || !account.CanAuthenticate)
        {
            passwordHasher.VerifyDummy(request.Password);
            return AuthenticationFailed();
        }

        var verification = passwordHasher.Verify(account.Id, credential.SecretHash, request.Password);
        if (verification == PasswordVerificationOutcome.Failed)
        {
            return AuthenticationFailed();
        }

        var now = clock.UtcNow;
        if (verification == PasswordVerificationOutcome.SuccessRehashNeeded)
        {
            var replacement = passwordHasher.Hash(account.Id, request.Password);
            await accountStore.UpdateCredentialHashAsync(lookupKeyHash, replacement, now, cancellationToken);
        }

        var session = sessionIssuer.Issue(account, request.Client, now);
        return IdentityOperationResult<AuthSessionResponse>.Success(session);
    }

    private static IdentityOperationResult<AuthSessionResponse> DuplicateRegistration()
    {
        return IdentityOperationResult<AuthSessionResponse>.Failed(
            IdentityFailureKind.Conflict,
            IdentityErrorCodes.IdentityConflict,
            "Registration could not be completed.",
            "An account cannot be created with the supplied identity information.");
    }

    private static IdentityOperationResult<AuthSessionResponse> AuthenticationFailed()
    {
        return IdentityOperationResult<AuthSessionResponse>.Failed(
            IdentityFailureKind.Authentication,
            IdentityErrorCodes.AuthenticationFailed,
            "Authentication failed.",
            "The supplied credentials could not be accepted.");
    }
}
