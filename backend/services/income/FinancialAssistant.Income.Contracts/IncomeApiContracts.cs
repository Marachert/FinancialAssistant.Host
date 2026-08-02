namespace FinancialAssistant.Income.Contracts;

public static class IncomeApiRoutes
{
    public const string Incomes = "/api/v1/incomes";
    public const string GatewayIncomes = "/incomes";
    public const string Income = "/api/v1/incomes/{incomeId}";
    public const string GatewayIncome = "/incomes/{incomeId}";
    public const string Archive = "/api/v1/incomes/{incomeId}/archive";
    public const string GatewayArchive = "/incomes/{incomeId}/archive";
    public const string Restore = "/api/v1/incomes/{incomeId}/restore";
    public const string GatewayRestore = "/incomes/{incomeId}/restore";
}

public static class IncomeGatewayHeaders
{
    public const string Authentication = "X-Gateway-Authentication";
    public const string UserId = "X-Gateway-User-Id";
}

public sealed record CreateIncomeRequest(
    decimal Amount,
    string? Currency,
    string? CategoryId,
    string? Merchant,
    DateOnly Date);

public sealed record UpdateIncomeRequest(
    decimal Amount,
    string? Currency,
    string? CategoryId,
    string? Merchant,
    DateOnly Date);

public sealed record IncomeRecordResponse(
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

public sealed record IncomeTotalResponse(string Currency, decimal Amount);

public sealed record IncomeListResponse(
    IReadOnlyList<IncomeRecordResponse> Records,
    IReadOnlyList<IncomeTotalResponse> ActiveTotals);

public sealed record IncomeApiErrorResponse(
    string? Title,
    string? Detail,
    int? Status,
    string? Code,
    string? TraceId);
