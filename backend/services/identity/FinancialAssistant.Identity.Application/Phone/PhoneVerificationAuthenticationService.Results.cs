using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Application.Phone;

public sealed partial class PhoneVerificationAuthenticationService
{
    private static IdentityOperationResult<PhoneVerificationStartResponse> InvalidStartRequest(
        IReadOnlyDictionary<string, string[]> errors)
    {
        var normalizedErrors = errors.Count == 0
            ? new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["phoneNumber"] = ["A valid E.164 phone number is required."]
            }
            : errors;
        return IdentityOperationResult<PhoneVerificationStartResponse>.Failed(
            IdentityFailureKind.Validation,
            IdentityErrorCodes.ValidationFailed,
            "Phone verification request is invalid.",
            "Correct the highlighted fields and submit the request again.",
            normalizedErrors);
    }

    private static IdentityOperationResult<AuthSessionResponse> InvalidConfirmRequest(
        IReadOnlyDictionary<string, string[]> errors)
    {
        return IdentityOperationResult<AuthSessionResponse>.Failed(
            IdentityFailureKind.Validation,
            IdentityErrorCodes.ValidationFailed,
            "Phone verification confirmation is invalid.",
            "Correct the highlighted fields and submit the request again.",
            errors);
    }

    private static IdentityOperationResult<PhoneVerificationStartResponse> RateLimitedStart(
        int? retryAfterSeconds)
    {
        return IdentityOperationResult<PhoneVerificationStartResponse>.Failed(
            IdentityFailureKind.RateLimited,
            IdentityErrorCodes.RateLimited,
            "Phone verification is temporarily limited.",
            "Wait before requesting another verification message.",
            retryAfterSeconds: retryAfterSeconds);
    }

    private static IdentityOperationResult<PhoneVerificationStartResponse> ProviderUnavailableStart()
    {
        return IdentityOperationResult<PhoneVerificationStartResponse>.Failed(
            IdentityFailureKind.ServiceUnavailable,
            IdentityErrorCodes.ProviderUnavailable,
            "Phone verification is temporarily unavailable.",
            "Try again later or use another configured authentication method.");
    }

    private static IdentityOperationResult<AuthSessionResponse> ProviderUnavailableConfirm()
    {
        return IdentityOperationResult<AuthSessionResponse>.Failed(
            IdentityFailureKind.ServiceUnavailable,
            IdentityErrorCodes.ProviderUnavailable,
            "Phone verification is temporarily unavailable.",
            "Try again later or use another configured authentication method.");
    }

    private static IdentityOperationResult<AuthSessionResponse> VerificationFailed()
    {
        return IdentityOperationResult<AuthSessionResponse>.Failed(
            IdentityFailureKind.Authentication,
            IdentityErrorCodes.ProviderAuthenticationFailed,
            "Phone verification failed.",
            "The verification could not be accepted.");
    }
}
