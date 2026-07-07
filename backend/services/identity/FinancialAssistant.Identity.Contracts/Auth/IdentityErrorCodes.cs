namespace FinancialAssistant.Identity.Contracts.Auth;

public static class IdentityErrorCodes
{
    public const string ValidationFailed = "validation_failed";
    public const string AuthenticationFailed = "authentication_failed";
    public const string ProviderAuthenticationFailed = "provider_authentication_failed";
    public const string ProviderLinkRequired = "provider_link_required";
    public const string ProviderUnavailable = "provider_unavailable";
    public const string SessionInvalid = "session_invalid";
    public const string SessionExpired = "session_expired";
    public const string SessionRevoked = "session_revoked";
    public const string IdentityConflict = "identity_conflict";
    public const string RateLimited = "rate_limited";
    public const string ServiceUnavailable = "service_unavailable";
    public const string NotImplemented = "not_implemented";

    public static readonly IReadOnlyList<string> All = Array.AsReadOnly(
        new[]
        {
            ValidationFailed,
            AuthenticationFailed,
            ProviderAuthenticationFailed,
            ProviderLinkRequired,
            ProviderUnavailable,
            SessionInvalid,
            SessionExpired,
            SessionRevoked,
            IdentityConflict,
            RateLimited,
            ServiceUnavailable,
            NotImplemented
        });
}
