namespace FinancialAssistant.Shared.Contracts.Events;

public static class FinancialScoreEventTypes
{
    public const int SchemaVersion = 1;
    public const string ScoreCalculated = "score.calculated.v1";
}

public sealed record FinancialScoreFactorV1(
    string Code,
    decimal Contribution);

public sealed record ScoreCalculatedV1(
    string CalculationId,
    string Currency,
    int Score,
    string FormulaVersion,
    IReadOnlyList<FinancialScoreFactorV1> Factors,
    DateTimeOffset CalculatedAtUtc);
