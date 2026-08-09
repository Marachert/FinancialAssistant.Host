namespace FinancialAssistant.FinancialScore.Domain;

public sealed class FinancialScoreCalculator
{
    private const int ObservationDays = 90;
    private const int TrendDays = 30;
    private const int CompletenessTargetDays = 30;

    public FinancialScoreCalculation Calculate(
        string calculationId,
        string sourceEventId,
        string userIdHash,
        string currency,
        IEnumerable<FinancialScoreRecordProjection> records,
        FinancialScoreProfileSettings profileSettings,
        DateTimeOffset calculatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(calculationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userIdHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(profileSettings);
        if (profileSettings.MonthlyBudgetAmount < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(profileSettings),
                "Monthly budget amount cannot be negative.");
        }

        if (calculatedAtUtc == default)
        {
            throw new ArgumentOutOfRangeException(nameof(calculatedAtUtc));
        }

        var observationEnd = DateOnly.FromDateTime(calculatedAtUtc.UtcDateTime);
        var observationStart = observationEnd.AddDays(-(ObservationDays - 1));
        var active = records
            .Where(item => item.Status == FinancialScoreProjectionStatuses.Active)
            .Where(item => item.Date >= observationStart && item.Date <= observationEnd)
            .ToArray();

        if (active.Length == 0)
        {
            return CreateNewUserCalculation(
                calculationId,
                sourceEventId,
                userIdHash,
                currency,
                profileSettings,
                calculatedAtUtc);
        }

        var currentMonthStart = new DateOnly(observationEnd.Year, observationEnd.Month, 1);
        var currentMonthExpense = Sum(
            active,
            FinancialScoreRecordTypes.Expense,
            currentMonthStart,
            observationEnd);
        var budgetUsage = CalculateBudgetUsage(
            currentMonthExpense,
            profileSettings.MonthlyBudgetAmount);

        var currentTrendStart = observationEnd.AddDays(-(TrendDays - 1));
        var previousTrendEnd = currentTrendStart.AddDays(-1);
        var previousTrendStart = previousTrendEnd.AddDays(-(TrendDays - 1));
        var currentTrendExpense = Sum(
            active,
            FinancialScoreRecordTypes.Expense,
            currentTrendStart,
            observationEnd);
        var previousTrendExpense = Sum(
            active,
            FinancialScoreRecordTypes.Expense,
            previousTrendStart,
            previousTrendEnd);
        var spendingTrend = CalculateSpendingTrend(
            currentTrendExpense,
            previousTrendExpense);

        var incomeConsistency = CalculateIncomeConsistency(active, observationEnd);
        var recordDays = active.Select(item => item.Date).Distinct().Count();
        var dataCompleteness = CalculateDataCompleteness(recordDays, profileSettings);

        var totalIncome = active
            .Where(item => item.RecordType == FinancialScoreRecordTypes.Income)
            .Sum(item => item.Amount);
        var totalExpense = active
            .Where(item => item.RecordType == FinancialScoreRecordTypes.Expense)
            .Sum(item => item.Amount);
        var raw = FinancialScoreFormula.BaseScore +
            budgetUsage.Contribution +
            spendingTrend.Contribution +
            incomeConsistency.Contribution +
            dataCompleteness;
        var policy = ApplyPolicies(
            raw,
            totalIncome,
            totalExpense,
            budgetUsage.UsageRatio);
        var score = (int)Math.Round(
            Math.Clamp(
                policy.AdjustedScore,
                FinancialScoreFormula.Minimum,
                FinancialScoreFormula.Maximum),
            0,
            MidpointRounding.AwayFromZero);

        var factors = new[]
        {
            new FinancialScoreFactor(
                "budget_usage",
                budgetUsage.Contribution,
                "Current calendar-month confirmed expense compared with the Profile monthly budget.",
                new[]
                {
                    Input("monthly_expense", currentMonthExpense, "currency"),
                    Input("monthly_budget", profileSettings.MonthlyBudgetAmount, "currency"),
                    Input("usage_percent", budgetUsage.UsageRatio * 100m, "percent")
                }),
            new FinancialScoreFactor(
                "spending_trend",
                spendingTrend.Contribution,
                "Current 30-day confirmed expense compared with the preceding 30 days.",
                new[]
                {
                    Input("current_30_day_expense", currentTrendExpense, "currency"),
                    Input("previous_30_day_expense", previousTrendExpense, "currency"),
                    Input("change_percent", spendingTrend.ChangePercent, "percent")
                }),
            new FinancialScoreFactor(
                "income_consistency",
                incomeConsistency.Contribution,
                "Variation in confirmed monthly income across the current and two preceding calendar months.",
                new[]
                {
                    Input("months_with_income", incomeConsistency.MonthsWithIncome, "count"),
                    Input("average_monthly_income", incomeConsistency.AverageIncome, "currency"),
                    Input(
                        "average_deviation_percent",
                        incomeConsistency.DeviationRatio * 100m,
                        "percent")
                }),
            new FinancialScoreFactor(
                "data_completeness",
                dataCompleteness,
                "Confirmed-record coverage and explicit Profile budget and onboarding settings.",
                new[]
                {
                    Input("confirmed_record_days", recordDays, "count"),
                    Input(
                        "budget_configured",
                        profileSettings.MonthlyBudgetAmount > 0m ? 1m : 0m,
                        "boolean"),
                    Input(
                        "profile_onboarding_completed",
                        profileSettings.ProfileOnboardingCompleted ? 1m : 0m,
                        "boolean"),
                    Input(
                        "preferences_onboarding_completed",
                        profileSettings.PreferencesOnboardingCompleted ? 1m : 0m,
                        "boolean")
                }),
            new FinancialScoreFactor(
                "penalty_cap",
                policy.Contribution,
                "Deterministic penalties and score caps for expense without income and severe budget overrun.",
                new[]
                {
                    Input(
                        "expense_without_income",
                        policy.ExpenseWithoutIncome ? 1m : 0m,
                        "boolean"),
                    Input(
                        "severe_budget_overrun",
                        policy.SevereBudgetOverrun ? 1m : 0m,
                        "boolean"),
                    Input("applied_cap", policy.AppliedCap ?? 0m, "score")
                })
        };

        return CreateCalculation(
            calculationId,
            sourceEventId,
            userIdHash,
            currency,
            score,
            factors,
            calculatedAtUtc);
    }

    private static FinancialScoreCalculation CreateNewUserCalculation(
        string calculationId,
        string sourceEventId,
        string userIdHash,
        string currency,
        FinancialScoreProfileSettings profileSettings,
        DateTimeOffset calculatedAtUtc)
    {
        var noInputs = Array.Empty<FinancialScoreFactorInput>();
        var factors = new[]
        {
            new FinancialScoreFactor(
                "budget_usage",
                0m,
                "Waiting for the first confirmed financial record.",
                noInputs),
            new FinancialScoreFactor(
                "spending_trend",
                0m,
                "Waiting for the first confirmed financial record.",
                noInputs),
            new FinancialScoreFactor(
                "income_consistency",
                0m,
                "Waiting for confirmed income history.",
                noInputs),
            new FinancialScoreFactor(
                "data_completeness",
                0m,
                "Profile settings are reported but do not move the new-user default.",
                new[]
                {
                    Input("confirmed_record_days", 0m, "count"),
                    Input(
                        "budget_configured",
                        profileSettings.MonthlyBudgetAmount > 0m ? 1m : 0m,
                        "boolean"),
                    Input(
                        "profile_onboarding_completed",
                        profileSettings.ProfileOnboardingCompleted ? 1m : 0m,
                        "boolean"),
                    Input(
                        "preferences_onboarding_completed",
                        profileSettings.PreferencesOnboardingCompleted ? 1m : 0m,
                        "boolean")
                }),
            new FinancialScoreFactor(
                "penalty_cap",
                0m,
                "New users remain neutral until a confirmed record is available.",
                new[] { Input("new_user_default", 1m, "boolean") })
        };

        return CreateCalculation(
            calculationId,
            sourceEventId,
            userIdHash,
            currency,
            FinancialScoreFormula.NewUserDefault,
            factors,
            calculatedAtUtc);
    }

    private static FinancialScoreCalculation CreateCalculation(
        string calculationId,
        string sourceEventId,
        string userIdHash,
        string currency,
        int score,
        IReadOnlyList<FinancialScoreFactor> factors,
        DateTimeOffset calculatedAtUtc) =>
        new(
            calculationId,
            sourceEventId,
            userIdHash.Trim(),
            currency.Trim().ToUpperInvariant(),
            score,
            FinancialScoreFormula.Version,
            factors,
            calculatedAtUtc.ToUniversalTime());

    private static decimal Sum(
        IEnumerable<FinancialScoreRecordProjection> records,
        string recordType,
        DateOnly start,
        DateOnly end) =>
        records
            .Where(item => item.RecordType == recordType)
            .Where(item => item.Date >= start && item.Date <= end)
            .Sum(item => item.Amount);

    private static BudgetUsageResult CalculateBudgetUsage(
        decimal expense,
        decimal monthlyBudget)
    {
        if (monthlyBudget <= 0m)
        {
            return new BudgetUsageResult(0m, 0m);
        }

        var ratio = expense / monthlyBudget;
        var contribution = ratio switch
        {
            <= 0.8m => 15m,
            <= 1m => (1m - ratio) * 75m,
            <= 1.5m => -(ratio - 1m) * 40m,
            _ => -20m
        };
        return new BudgetUsageResult(Round(contribution), ratio);
    }

    private static SpendingTrendResult CalculateSpendingTrend(
        decimal currentExpense,
        decimal previousExpense)
    {
        if (previousExpense == 0m)
        {
            return currentExpense == 0m
                ? new SpendingTrendResult(0m, 0m)
                : new SpendingTrendResult(-5m, 100m);
        }

        var changeRatio = (currentExpense - previousExpense) / previousExpense;
        return new SpendingTrendResult(
            Round(Math.Clamp(-changeRatio, -1m, 1m) * 10m),
            Round(changeRatio * 100m));
    }

    private static IncomeConsistencyResult CalculateIncomeConsistency(
        IReadOnlyCollection<FinancialScoreRecordProjection> records,
        DateOnly observationEnd)
    {
        var currentMonth = new DateOnly(observationEnd.Year, observationEnd.Month, 1);
        var incomes = Enumerable.Range(0, 3)
            .Select(offset =>
            {
                var start = currentMonth.AddMonths(-offset);
                return Sum(
                    records,
                    FinancialScoreRecordTypes.Income,
                    start,
                    start.AddMonths(1).AddDays(-1));
            })
            .ToArray();
        var monthsWithIncome = incomes.Count(item => item > 0m);
        var average = incomes.Average();
        if (monthsWithIncome < 2 || average == 0m)
        {
            return new IncomeConsistencyResult(0m, Round(average), 1m, monthsWithIncome);
        }

        var deviationRatio = incomes
            .Select(item => Math.Abs(item - average))
            .Average() / average;
        return new IncomeConsistencyResult(
            Round((1m - Math.Min(1m, deviationRatio)) * 15m),
            Round(average),
            Round(deviationRatio),
            monthsWithIncome);
    }

    private static decimal CalculateDataCompleteness(
        int recordDays,
        FinancialScoreProfileSettings profileSettings)
    {
        var recordCoverage = Math.Min(
            6m,
            recordDays * 6m / CompletenessTargetDays);
        var profileCoverage =
            (profileSettings.MonthlyBudgetAmount > 0m ? 2m : 0m) +
            (profileSettings.ProfileOnboardingCompleted ? 1m : 0m) +
            (profileSettings.PreferencesOnboardingCompleted ? 1m : 0m);
        return Round(recordCoverage + profileCoverage);
    }

    private static PolicyResult ApplyPolicies(
        decimal raw,
        decimal totalIncome,
        decimal totalExpense,
        decimal budgetUsageRatio)
    {
        var expenseWithoutIncome = totalIncome == 0m && totalExpense > 0m;
        var severeBudgetOverrun = budgetUsageRatio >= 1.5m;
        var adjusted = raw - (expenseWithoutIncome ? 15m : 0m);
        decimal? cap = expenseWithoutIncome ? 39m : null;
        if (severeBudgetOverrun)
        {
            cap = cap is null ? 49m : Math.Min(cap.Value, 49m);
        }

        if (cap is not null)
        {
            adjusted = Math.Min(adjusted, cap.Value);
        }

        return new PolicyResult(
            adjusted,
            Round(adjusted - raw),
            expenseWithoutIncome,
            severeBudgetOverrun,
            cap);
    }

    private static FinancialScoreFactorInput Input(
        string code,
        decimal value,
        string unit) =>
        new(code, Round(value), unit);

    private static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record BudgetUsageResult(decimal Contribution, decimal UsageRatio);

    private sealed record SpendingTrendResult(decimal Contribution, decimal ChangePercent);

    private sealed record IncomeConsistencyResult(
        decimal Contribution,
        decimal AverageIncome,
        decimal DeviationRatio,
        int MonthsWithIncome);

    private sealed record PolicyResult(
        decimal AdjustedScore,
        decimal Contribution,
        bool ExpenseWithoutIncome,
        bool SevereBudgetOverrun,
        decimal? AppliedCap);
}
