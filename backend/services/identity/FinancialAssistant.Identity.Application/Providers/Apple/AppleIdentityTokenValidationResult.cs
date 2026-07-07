namespace FinancialAssistant.Identity.Application.Providers.Apple;

public sealed record AppleIdentityPrincipal(
    string Subject,
    string? Email,
    bool EmailVerified,
    bool IsPrivateEmail);

public sealed record AppleIdentityTokenValidationResult(
    AppleIdentityValidationStatus Status,
    AppleIdentityPrincipal? Principal)
{
    public static AppleIdentityTokenValidationResult Valid(AppleIdentityPrincipal principal) =>
        new(AppleIdentityValidationStatus.Valid, principal);

    public static AppleIdentityTokenValidationResult Invalid() =>
        new(AppleIdentityValidationStatus.Invalid, null);

    public static AppleIdentityTokenValidationResult Unavailable() =>
        new(AppleIdentityValidationStatus.Unavailable, null);
}

public enum AppleIdentityValidationStatus
{
    Valid = 1,
    Invalid = 2,
    Unavailable = 3
}
