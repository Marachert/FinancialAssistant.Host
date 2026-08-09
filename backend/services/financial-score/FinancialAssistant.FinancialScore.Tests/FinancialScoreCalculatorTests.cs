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
            Record("income-current", FinancialScoreRecordTypes.Income, 1000m, new DateOnly(2026, 8, 1)),
            Record("expense-current", FinancialScoreRecordTypes.Expense, 400m, new DateOnly(2026, 8, 2)),
            Record("income-previous", FinancialScoreRecordTypes.Income, 1000m, new DateOnly(2026, 7, 1)),
            Record("expense-previous", FinancialScoreRecordTypes.Expense, 500m, new DateOnly(2026, 7, 2))
        };
        var settings = new FinancialScoreProfileSettings(1000m, true, true);

        var first = calculator.Calculate(
            "score:event-1",
            "event-1",
            "synthetic-owner-hash",
            "usd",
            records,
            settings,
            CalculatedAt);
        var second = calculator.Calculate(
            "score:event-1",
            "event-1",
            "synthetic-owner-hash",
            "usd",
            records.Reverse(),
            settings,
            CalculatedAt);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.Equal(FinancialScoreFormula.Version, first.FormulaVersion);
        Assert.Equal(5, first.Factors.Count);
        Assert.Equal(
            new[]
            {
                "budget_usage",
                "spending_trend",
                "income_consistency",
                "data_completeness",
                "penalty_cap"
            },
            first.Factors.Select(item => item.Code));
        Assert.InRange(first.Score, FinancialScoreFormula.Minimum, FinancialScoreFormula.Maximum);
        Assert.All(first.Factors, item => Assert.False(string.IsNullOrWhiteSpace(item.Explanation)));
        Assert.Contains(
            first.Factors.Single(item => item.Code == "budget_usage").Inputs,
            item => item.Code == "monthly_budget" && item.Value == 1000m);
    }

    [Fact]
    public void Calculate_AppliesBudgetAndExplicitCap()
    {
        var calculator = new FinancialScoreCalculator();
        var records = new[]
        {
            Record("income-june", FinancialScoreRecordTypes.Income, 1000m, new DateOnly(2026, 6, 10)),
            Record("income-july", FinancialScoreRecordTypes.Income, 1000m, new DateOnly(2026, 7, 10)),
            Record("income-august", FinancialScoreRecordTypes.Income, 1000m, new DateOnly(2026, 8, 10)),
            Record("expense-july", FinancialScoreRecordTypes.Expense, 100m, new DateOnly(2026, 7, 15)),
            Record("expense-august", FinancialScoreRecordTypes.Expense, 1600m, new DateOnly(2026, 8, 15))
        };

        var result = calculator.Calculate(
            "score:event-2",
            "event-2",
            "synthetic-owner-hash",
            "USD",
            records,
            new FinancialScoreProfileSettings(1000m, true, true),
            CalculatedAt);

        Assert.True(result.Score <= 49);
        Assert.Equal(
            -20m,
            result.Factors.Single(item => item.Code == "budget_usage").Contribution);
        var policy = result.Factors.Single(item => item.Code == "penalty_cap");
        Assert.Contains(
            policy.Inputs,
            item => item.Code == "severe_budget_overrun" && item.Value == 1m);
        Assert.Contains(
            policy.Inputs,
            item => item.Code == "applied_cap" && item.Value == 49m);
    }

    [Fact]
    public void Calculate_UsesNeutralDefaultForNewUser()
    {
        var result = new FinancialScoreCalculator().Calculate(
            "score:new-user",
            "new-user",
            "synthetic-owner-hash",
            "USD",
            Array.Empty<FinancialScoreRecordProjection>(),
            new FinancialScoreProfileSettings(500m, true, true),
            CalculatedAt);

        Assert.Equal(FinancialScoreFormula.NewUserDefault, result.Score);
        Assert.All(result.Factors, item => Assert.Equal(0m, item.Contribution));
        Assert.Contains(
            result.Factors.Single(item => item.Code == "penalty_cap").Inputs,
            item => item.Code == "new_user_default" && item.Value == 1m);
    }

    [Fact]
    public void Calculate_ExpenseWithoutIncomeIsPenalizedAndCapped()
    {
        var result = new FinancialScoreCalculator().Calculate(
            "score:expense-only",
            "expense-only",
            "synthetic-owner-hash",
            "USD",
            new[]
            {
                Record(
                    "expense-only",
                    FinancialScoreRecordTypes.Expense,
                    25m,
                    new DateOnly(2026, 8, 20))
            },
            FinancialScoreProfileSettings.Unconfigured,
            CalculatedAt);

        Assert.True(result.Score <= 39);
        var policy = result.Factors.Single(item => item.Code == "penalty_cap");
        Assert.True(policy.Contribution < 0m);
        Assert.Contains(
            policy.Inputs,
            item => item.Code == "expense_without_income" && item.Value == 1m);
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
            FinancialScoreProfileSettings.Unconfigured,
            CalculatedAt);
        var ignored = new[]
        {
            Record(
                "archived",
                FinancialScoreRecordTypes.Expense,
                999m,
                new DateOnly(2026, 8, 20),
                FinancialScoreProjectionStatuses.Archived),
            Record("old", FinancialScoreRecordTypes.Expense, 999m, new DateOnly(2025, 1, 1))
        };
        var result = calculator.Calculate(
            "score:event-4",
            "event-4",
            "synthetic-owner-hash",
            "USD",
            ignored,
            FinancialScoreProfileSettings.Unconfigured,
            CalculatedAt);

        Assert.Equal(JsonSerializer.Serialize(baseline), JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Calculate_RejectsInvalidProfileBudget()
    {
        var calculator = new FinancialScoreCalculator();

        Assert.Throws<ArgumentOutOfRangeException>(() => calculator.Calculate(
            "score:invalid-profile",
            "invalid-profile",
            "synthetic-owner-hash",
            "USD",
            Array.Empty<FinancialScoreRecordProjection>(),
            new FinancialScoreProfileSettings(-1m, false, false),
            CalculatedAt));
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
