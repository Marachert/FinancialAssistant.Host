using FinancialAssistant.Identity.Application.Authentication;
using FinancialAssistant.Identity.Contracts.Auth;

namespace FinancialAssistant.Identity.Application.Providers.Apple;

public sealed partial class AppleProviderAuthenticationService
{
    private static IdentityOperationResult<AuthSessionResponse> InvalidRequest(
        IReadOnlyDictionary<string, string[]> errors)
    {
        return IdentityOperationResult<AuthSessionResponse>.Failed(
            IdentityFailureKind.Validation,
            IdentityErrorCodes.ValidationFailed,
            "Apple sign-in request is invalid.",
            "Correct the highlighted fields and submit the request again.",
            errors);
    }

    private static IdentityOperationResult<AuthSessionResponse> ProviderAuthenticationFailed()
    {
        return IdentityOperationResult<AuthSessionResponse>.Failed(
            IdentityFailureKind.Authentication,
            IdentityErrorCodes.ProviderAuthenticationFailed,
            "Provider authentication failed.",
            "The supplied provider credential could not be accepted.");
    }

    private static IdentityOperationResult<AuthSessionResponse> ProviderLinkRequired()
    {
        return IdentityOperationResult<AuthSessionResponse>.Failed(
            IdentityFailureKind.Conflict,
            IdentityErrorCodes.ProviderLinkRequired,
            "Account linking is required.",
            "Sign in with the existing account before linking this Apple identity.");
    }

    private static IdentityOperationResult<AuthSessionResponse> ProviderUnavailable()
    {
        return IdentityOperationResult<AuthSessionResponse>.Failed(
            IdentityFailureKind.ServiceUnavailable,
            IdentityErrorCodes.ProviderUnavailable,
            "Apple sign-in is temporarily unavailable.",
            "Try again later or use another configured authentication method.");
    }
}
