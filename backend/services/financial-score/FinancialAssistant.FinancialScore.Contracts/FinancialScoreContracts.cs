namespace FinancialAssistant.FinancialScore.Contracts;

public static class FinancialScoreApiRoutes
{
    public const string Current = "/api/v1/financial-score/current";
    public const string History = "/api/v1/financial-score/history";
    public const string GatewayCurrent = "/financial-score/current";
    public const string GatewayHistory = "/financial-score/history";
}

public static class FinancialScoreGatewayHeaders
{
    public const string Authentication = "X-Gateway-Authentication";
    public const string UserId = "X-Gateway-User-Id";
}

public sealed record FinancialScoreFactorInputResponse(
    string Code,
    decimal Value,
    string Unit);

public sealed record FinancialScoreFactorResponse(
    string Code,
    decimal Contribution,
    string Explanation,
    IReadOnlyList<FinancialScoreFactorInputResponse> Inputs);

public sealed record FinancialScoreResponse(
    string CalculationId,
    string Currency,
    int Score,
    string FormulaVersion,
    IReadOnlyList<FinancialScoreFactorResponse> Factors,
    DateTimeOffset CalculatedAtUtc);

public sealed record FinancialScoreHistoryResponse(
    IReadOnlyList<FinancialScoreResponse> Items,
    int Limit,
    bool HasMore,
    DateTimeOffset? NextBeforeUtc,
    string? NextBeforeCalculationId);

public sealed record FinancialScoreApiErrorResponse(
    string? Title,
    string? Detail,
    int? Status,
    string? Code,
    string? TraceId);

public sealed record FinancialScoreServiceInfoResponse(
    string Service,
    string Status,
    string Environment,
    string StorageProvider,
    string FormulaVersion);
