using System.ComponentModel.DataAnnotations;

namespace FinancialAssistant.Identity.Contracts.Auth;

public sealed record PhoneVerificationStartRequest(
    [property: Required, StringLength(32, MinimumLength = 8)] string PhoneNumber,
    [property: Required, StringLength(32, MinimumLength = 4)] string Purpose,
    [property: Required] IdentityClientContext Client);

public static class PhoneVerificationPurposes
{
    public const string SignIn = "sign_in";
}
