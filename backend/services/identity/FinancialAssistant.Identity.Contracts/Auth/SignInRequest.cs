using System.ComponentModel.DataAnnotations;

namespace FinancialAssistant.Identity.Contracts.Auth;

public sealed record SignInRequest(
    [property: Required, EmailAddress, StringLength(320)] string Email,
    [property: Required, StringLength(128, MinimumLength = 12)] string Password,
    [property: Required] IdentityClientContext Client);
