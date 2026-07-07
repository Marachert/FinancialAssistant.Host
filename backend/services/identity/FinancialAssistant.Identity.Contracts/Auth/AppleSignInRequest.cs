using System.ComponentModel.DataAnnotations;

namespace FinancialAssistant.Identity.Contracts.Auth;

public sealed record AppleSignInRequest(
    [property: Required, StringLength(16384, MinimumLength = 64)] string IdentityToken,
    [property: Required, StringLength(512, MinimumLength = 16)] string Nonce,
    [property: Required] IdentityClientContext Client);
