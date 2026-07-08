namespace FinancialAssistant.Identity.Api.RateLimiting;

internal static class IdentityRateLimitPolicies
{
    public const string Registration = "identity-registration";
    public const string SignIn = "identity-sign-in";
    public const string ProviderSignIn = "identity-provider-sign-in";
    public const string PhoneStart = "identity-phone-start";
    public const string PhoneConfirm = "identity-phone-confirm";
    public const string Session = "identity-session";
}
