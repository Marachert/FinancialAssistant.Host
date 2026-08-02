namespace FinancialAssistant.Expense.Contracts;

public sealed record ExpenseServiceInfoResponse(
    string Service,
    string Status,
    string Environment,
    string StorageProvider,
    string AuthoritativeInput);
