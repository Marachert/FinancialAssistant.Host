namespace FinancialAssistant.Identity.Contracts.Auth;

public sealed record IdentityApiErrorResponse(
    string Type,
    string Title,
    int Status,
    string Code,
    string Detail,
    string TraceId,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    int? RetryAfterSeconds = null);
