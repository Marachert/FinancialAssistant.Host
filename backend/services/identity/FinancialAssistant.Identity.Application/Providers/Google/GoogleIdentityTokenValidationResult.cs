namespace FinancialAssistant.Identity.Application.Providers.Google;

public sealed record GoogleIdentityPrincipal(
    string Subject,
    string? Email,
    bool EmailVerified,
    string? HostedDomain);

public sealed record GoogleIdentityTokenValidationResult(
    GoogleIdentityValidationStatus Status,
    GoogleIdentityPrincipal? Principal)
{
    public static GoogleIdentityTokenValidationResult Valid(GoogleIdentityPrincipal principal) =>
        new(GoogleIdentityValidationStatus.Valid, principal);

    public static GoogleIdentityTokenValidationResult Invalid() =>
        new(GoogleIdentityValidationStatus.Invalid, null);

    public static GoogleIdentityTokenValidationResult Unavailable() =>
        new(GoogleIdentityValidationStatus.Unavailable, null);
}

public enum GoogleIdentityValidationStatus
{
    Valid = 1,
    Invalid = 2,
    Unavailable = 3
}
