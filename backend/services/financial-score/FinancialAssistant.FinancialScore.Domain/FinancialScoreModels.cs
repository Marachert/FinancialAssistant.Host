namespace FinancialAssistant.FinancialScore.Domain;

public static class FinancialScoreFormula
{
    public const string Version = "financial-score-v2";
    public const int Minimum = 0;
    public const int Maximum = 100;
    public const int NewUserDefault = 50;
    public const decimal BaseScore = 50m;
}

public static class FinancialScoreRecordTypes
{
    public const string Income = "income";
    public const string Expense = "expense";
}

public static class FinancialScoreProjectionStatuses
{
    public const string Active = "active";
    public const string Archived = "archived";
}

public sealed record FinancialScoreRecordProjection(
    string RecordType,
    string RecordId,
    string UserIdHash,
    decimal Amount,
    string Currency,
    DateOnly Date,
    string Status,
    long Revision,
    DateTimeOffset ChangedAtUtc,
    string EventId);

public sealed record FinancialScoreProfileSettings(
    decimal MonthlyBudgetAmount,
    bool ProfileOnboardingCompleted,
    bool PreferencesOnboardingCompleted)
{
    public static FinancialScoreProfileSettings Unconfigured { get; } =
        new(0m, false, false);
}

public sealed record FinancialScoreSemanticFactor(
    string Code,
    decimal Adjustment);

public sealed record FinancialScoreFactorInput(
    string Code,
    decimal Value,
    string Unit);

public sealed record FinancialScoreFactor(
    string Code,
    decimal Contribution,
    string Explanation,
    IReadOnlyList<FinancialScoreFactorInput> Inputs);

public sealed record FinancialScoreCalculation(
    string CalculationId,
    string SourceEventId,
    string UserIdHash,
    string Currency,
    int Score,
    string FormulaVersion,
    IReadOnlyList<FinancialScoreFactor> Factors,
    DateTimeOffset CalculatedAtUtc);

public sealed record FinancialScoreSnapshot(
    string UserIdHash,
    string Currency,
    IReadOnlyList<FinancialScoreRecordProjection> Records);

public enum FinancialScoreProjectionWriteResult
{
    Applied,
    Duplicate,
    Stale
}

public sealed record FinancialScoreProjectionWriteOutcome(
    FinancialScoreProjectionWriteResult Result,
    string? PreviousCurrency);
