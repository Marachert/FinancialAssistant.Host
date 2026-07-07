using System.ComponentModel.DataAnnotations;

namespace FinancialAssistant.Identity.Contracts.Auth;

public sealed record IdentityClientContext(
    [property: Required, StringLength(128, MinimumLength = 8)] string ClientInstanceId,
    [property: Required, StringLength(32, MinimumLength = 2)] string Platform,
    [property: StringLength(64)] string? AppVersion);
