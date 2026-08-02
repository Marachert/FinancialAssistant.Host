namespace FinancialAssistant.Income.Contracts;

public sealed record IncomeServiceInfoResponse(
    string Service,
    string Status,
    string Environment,
    string StorageProvider,
    string AuthoritativeInput);
