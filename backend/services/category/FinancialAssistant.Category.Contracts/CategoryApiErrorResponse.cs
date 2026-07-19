namespace FinancialAssistant.Category.Contracts;

public sealed record CategoryApiErrorResponse(
    string Type,
    string Title,
    int Status,
    string Detail,
    string Code,
    string? TraceId);
