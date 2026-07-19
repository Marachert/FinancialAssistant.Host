namespace FinancialAssistant.TransactionIntake.Contracts;

public sealed record TransactionIntakeErrorResponse(
    string? Type,
    string Title,
    int Status,
    string? Detail,
    string? Code,
    string? TraceId);
