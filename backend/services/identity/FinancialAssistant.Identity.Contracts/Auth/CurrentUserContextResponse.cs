namespace FinancialAssistant.Identity.Contracts.Auth;

public sealed record CurrentUserContextResponse(
    string UserId,
    string SessionId,
    IReadOnlyList<string> Roles,
    string AuthenticationMethod,
    DateTimeOffset AuthenticatedAtUtc,
    DateTimeOffset SessionExpiresAtUtc);
