using System.Text.Json;
using FinancialAssistant.FinancialScore.Domain;
using Xunit;

namespace FinancialAssistant.FinancialScore.Tests;

public sealed class FinancialScoreCalculatorTests
{
    private static readonly DateTimeOffset CalculatedAt =
        new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Calculate_IsVersionedDeterministicAndTransparent()
    {
        var calculator = new FinancialScoreCalculator();
        var records = new[]
        {
            Record("income", FinancialScoreRecordTypes.Income, 1000m, new DateOnly(2026, 8, 1)),
            Record("expense", FinancialScoreRecordTypes.Expense, 400m, new DateOnly(2026, 8, 2))
        };

        var first = calculator.Calculate(
            "score:event-1",
            "event-1",
            "synthetic-owner-hash",
            "usd",
            records,
            semanticFactors: null,
            CalculatedAt);
        var second = calculator.Calculate(
            "score:event-1",
            "event-1",
            "synthetic-owner-hash",
            "usd",
            records.Reverse(),
            semanticFactors: null,
            CalculatedAt);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(FinancialScoreFormula.Version, first.FormulaVersion);
        Assert.Equal(4, first.Factors.Count);
        Assert.InRange(first.Score, FinancialScoreFormula.Minimum, FinancialScoreFormula.Maximum);
        Assert.Equal(18m, first.Factors.Single(item => item.Code == "cash_flow").Contribution);
    }

    [Fact]
    public void Calculate_BoundsSemanticInputsAndNeverAcceptsFinalScore()
    {
        var calculator = new FinancialScoreCalculator();
        var factors = new[]
        {
            new FinancialScoreSemanticFactor("recurring_income", 2m),
            new FinancialScoreSemanticFactor("expense_description_quality", 2m),
            new FinancialScoreSemanticFactor("merchant_stability", 2m)
        };

        var result = calculator.Calculate(
            "score:event-2",
            "event-2",
            "synthetic-owner-hash",
            "USD",
            Array.Empty<FinancialScoreRecordProjection>(),
            factors,
            CalculatedAt);

        Assert.Equal(
            FinancialScoreFormula.MaximumSemanticAdjustment,
            result.Factors.Single(item => item.Code == "bounded_semantic").Contribution);
        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calculate(
            "score:event-3",
            "event-3",
            "synthetic-owner-hash",
            "USD",
            Array.Empty<FinancialScoreRecordProjection>(),
            new[] { new FinancialScoreSemanticFactor("unbounded", 2.01m) },
            CalculatedAt));
    }

    [Fact]
    public void Calculate_IgnoresArchivedAndOutOfWindowRecords()
    {
        var calculator = new FinancialScoreCalculator();
        var baseline = calculator.Calculate(
            "score:event-4",
            "event-4",
            "synthetic-owner-hash",
            "USD",
            Array.Empty<FinancialScoreRecordProjection>(),
            null,
            CalculatedAt);
        var ignored = new[]
        {
            Record("archived", FinancialScoreRecordTypes.Expense, 999m, new DateOnly(2026, 8, 20), "archived"),
            Record("old", FinancialScoreRecordTypes.Expense, 999m, new DateOnly(2025, 1, 1))
        };
        var result = calculator.Calculate(
            "score:event-4",
            "event-4",
            "synthetic-owner-hash",
            "USD",
            ignored,
            null,
            CalculatedAt);

        Assert.Equal(JsonSerializer.Serialize(baseline), JsonSerializer.Serialize(result));
    }

    private static FinancialScoreRecordProjection Record(
        string id,
        string type,
        decimal amount,
        DateOnly date,
        string status = FinancialScoreProjectionStatuses.Active) =>
        new(
            type,
            id,
            "synthetic-owner-hash",
            amount,
            "USD",
            date,
            status,
            0,
            CalculatedAt,
            $"event-{id}");
}
