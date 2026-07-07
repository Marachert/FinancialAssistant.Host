using System.ComponentModel.DataAnnotations;

namespace FinancialAssistant.Identity.Contracts.Auth;

public sealed record PhoneVerificationConfirmRequest(
    [property: Required, StringLength(64, MinimumLength = 16)] string VerificationId,
    [property: Required, StringLength(10, MinimumLength = 4)] string Code,
    [property: Required] IdentityClientContext Client);
