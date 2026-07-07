using System.ComponentModel.DataAnnotations;

namespace FinancialAssistant.Identity.Contracts.Auth;

public sealed record RefreshSessionRequest(
    [property: Required, StringLength(4096, MinimumLength = 32)] string RefreshToken,
    [property: Required] IdentityClientContext Client);
