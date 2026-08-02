namespace FinancialAssistant.Expense.Contracts;

public static class ExpenseApiRoutes
{
    public const string Expenses = "/api/v1/expenses";
    public const string GatewayExpenses = "/expenses";
    public const string Expense = "/api/v1/expenses/{expenseId}";
    public const string GatewayExpense = "/expenses/{expenseId}";
    public const string Archive = "/api/v1/expenses/{expenseId}/archive";
    public const string GatewayArchive = "/expenses/{expenseId}/archive";
    public const string Restore = "/api/v1/expenses/{expenseId}/restore";
    public const string GatewayRestore = "/expenses/{expenseId}/restore";
}

public static class ExpenseGatewayHeaders
{
    public const string Authentication = "X-Gateway-Authentication";
    public const string UserId = "X-Gateway-User-Id";
}

public sealed record CreateExpenseRequest(
    decimal Amount,
    string? Currency,
    string? CategoryId,
    string? Merchant,
    DateOnly Date);

public sealed record UpdateExpenseRequest(
    decimal Amount,
    string? Currency,
    string? CategoryId,
    string? Merchant,
    DateOnly Date);

public sealed record ExpenseRecordResponse(
    string Id,
    string Status,
    string Origin,
    decimal Amount,
    string Currency,
    string CategoryId,
    string? Merchant,
    DateOnly Date,
    DateTimeOffset ConfirmedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    long Revision);

public sealed record ExpenseTotalResponse(string Currency, decimal Amount);

public sealed record ExpenseListResponse(
    IReadOnlyList<ExpenseRecordResponse> Records,
    IReadOnlyList<ExpenseTotalResponse> ActiveTotals);

public sealed record ExpenseApiErrorResponse(
    string? Title,
    string? Detail,
    int? Status,
    string? Code,
    string? TraceId);
