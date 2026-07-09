namespace FinancialAssistant.Profile.Contracts;

public sealed record ProfileApiErrorResponse(
    string Code,
    string Message,
    string? TraceId);
