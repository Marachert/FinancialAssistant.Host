namespace FinancialAssistant.Profile.Contracts;

public sealed record ProfileApiErrorResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Code,
    string? TraceId);
