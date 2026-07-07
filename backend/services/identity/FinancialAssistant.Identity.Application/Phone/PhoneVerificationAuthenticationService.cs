using FinancialAssistant.Identity.Application.Abstractions;
using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Application.Phone;

public sealed partial class PhoneVerificationAuthenticationService : IPhoneVerificationAuthenticationService
{
    private const string ProviderName = "phone";
    private const string AuthenticationMethod = "phone_otp";
    private readonly IPhoneVerificationProvider provider;
    private readonly IPhoneVerificationChallengeStore challengeStore;
    private readonly IIdentityProviderIdentifierHasher identifierHasher;
    private readonly IIdentityFederatedAccountStore federatedAccountStore;
    private readonly IIdentityAccountStore accountStore;
    private readonly IInitialSessionIssuer sessionIssuer;
    private readonly IIdentityEventPublisher eventPublisher;
    private readonly ISystemClock clock;
    private readonly PhoneVerificationPolicy policy;

    public PhoneVerificationAuthenticationService(
        IPhoneVerificationProvider provider,
        IPhoneVerificationChallengeStore challengeStore,
        IIdentityProviderIdentifierHasher identifierHasher,
        IIdentityFederatedAccountStore federatedAccountStore,
        IIdentityAccountStore accountStore,
        IInitialSessionIssuer sessionIssuer,
        IIdentityEventPublisher eventPublisher,
        ISystemClock clock,
        PhoneVerificationPolicy policy)
    {
        this.provider = provider;
        this.challengeStore = challengeStore;
        this.identifierHasher = identifierHasher;
        this.federatedAccountStore = federatedAccountStore;
        this.accountStore = accountStore;
        this.sessionIssuer = sessionIssuer;
        this.eventPublisher = eventPublisher;
        this.clock = clock;
        this.policy = policy;
    }

    public async Task<IdentityOperationResult<PhoneVerificationStartResponse>> StartAsync(
        PhoneVerificationStartRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = IdentityRequestValidator.ValidatePhoneVerificationStart(request);
        if (validationErrors.Count > 0
            || !PhoneNumberNormalizer.TryNormalize(request.PhoneNumber, out var phoneNumber))
        {
            return InvalidStartRequest(validationErrors);
        }

        var now = clock.UtcNow;
        var phoneHash = identifierHasher.Hash(ProviderName, "number", phoneNumber);
        var clientHash = identifierHasher.Hash(
            ProviderName,
            "client_instance",
            request.Client.ClientInstanceId);
        var challenge = new PhoneVerificationChallengeRecord(
            Guid.NewGuid().ToString("N"),
            phoneHash,
            clientHash,
            request.Purpose,
            null,
            now,
            now.Add(policy.ChallengeLifetime),
            now.Add(policy.ResendCooldown),
            0,
            PhoneVerificationChallengeStatus.PendingDispatch);

        var reservation = await challengeStore.TryReserveAsync(
            challenge,
            policy,
            cancellationToken);
        if (!reservation.Reserved)
        {
            return RateLimitedStart(reservation.RetryAfterSeconds);
        }

        var dispatch = await provider.StartAsync(
            new PhoneVerificationDispatchRequest(
                challenge.Id,
                phoneNumber,
                request.Purpose,
                correlationId),
            cancellationToken);
        if (dispatch.Status != PhoneVerificationDispatchStatus.Accepted
            || string.IsNullOrWhiteSpace(dispatch.ProviderReference))
        {
            await challengeStore.CancelAsync(challenge.Id, cancellationToken);
            return dispatch.Status == PhoneVerificationDispatchStatus.RateLimited
                ? RateLimitedStart(dispatch.RetryAfterSeconds)
                : ProviderUnavailableStart();
        }

        await challengeStore.ActivateAsync(
            challenge.Id,
            dispatch.ProviderReference,
            cancellationToken);
        return IdentityOperationResult<PhoneVerificationStartResponse>.Success(
            new PhoneVerificationStartResponse(
                challenge.Id,
                PhoneNumberNormalizer.Mask(phoneNumber),
                challenge.ExpiresAtUtc,
                challenge.ResendAvailableAtUtc));
    }

    public async Task<IdentityOperationResult<AuthSessionResponse>> ConfirmAsync(
        PhoneVerificationConfirmRequest request,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        var validationErrors = IdentityRequestValidator.ValidatePhoneVerificationConfirm(
            request,
            policy.CodeLength);
        if (validationErrors.Count > 0)
        {
            return InvalidConfirmRequest(validationErrors);
        }

        var now = clock.UtcNow;
        var challenge = await challengeStore.FindAsync(request.VerificationId, cancellationToken);
        var clientHash = identifierHasher.Hash(
            ProviderName,
            "client_instance",
            request.Client.ClientInstanceId);
        if (challenge is null
            || challenge.Status != PhoneVerificationChallengeStatus.Active
            || challenge.ExpiresAtUtc <= now
            || !string.Equals(challenge.Purpose, PhoneVerificationPurposes.SignIn, StringComparison.Ordinal)
            || !string.Equals(challenge.ClientInstanceHash, clientHash, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(challenge.ProviderReference))
        {
            await PublishFailureAsync(correlationId, cancellationToken);
            return VerificationFailed();
        }

        var check = await provider.CheckAsync(
            challenge.ProviderReference,
            request.Code,
            cancellationToken);
        if (check.Status == PhoneVerificationCheckStatus.Unavailable)
        {
            return ProviderUnavailableConfirm();
        }

        if (check.Status != PhoneVerificationCheckStatus.Approved)
        {
            await challengeStore.RecordRejectedAttemptAsync(
                challenge.Id,
                policy.MaximumAttempts,
                cancellationToken);
            await PublishFailureAsync(correlationId, cancellationToken);
            return VerificationFailed();
        }

        var completed = await challengeStore.TryCompleteAsync(
            challenge.Id,
            now,
            cancellationToken);
        if (!completed)
        {
            await PublishFailureAsync(correlationId, cancellationToken);
            return VerificationFailed();
        }

        return await CompleteAuthenticationAsync(
            challenge.PhoneSubjectHash,
            request.Client,
            correlationId,
            now,
            cancellationToken);
    }
}
