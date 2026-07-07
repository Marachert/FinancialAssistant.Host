using System.ComponentModel.DataAnnotations;

namespace FinancialAssistant.Identity.Contracts.Auth;

public sealed record GoogleSignInRequest(
    [property: Required, StringLength(16384, MinimumLength = 64)] string IdToken,
    [property: Required] IdentityClientContext Client);
