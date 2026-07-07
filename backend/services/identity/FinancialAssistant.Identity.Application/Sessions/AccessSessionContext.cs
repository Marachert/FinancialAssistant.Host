namespace FinancialAssistant.Identity.Application.Sessions;

public sealed record AccessSessionContext(
    string AccountId,
    string SessionId,
    string AuthenticationMethod,
    DateTimeOffset AuthenticatedAtUtc,
    DateTimeOffset ExpiresAtUtc);
