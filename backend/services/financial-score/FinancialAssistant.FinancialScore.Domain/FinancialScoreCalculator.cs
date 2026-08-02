namespace FinancialAssistant.FinancialScore.Domain;

public sealed class FinancialScoreCalculator
{
    private const int ObservationDays = 90;
    private const int TrackingTargetDays = 30;

    public FinancialScoreCalculation Calculate(
        string calculationId,
        string sourceEventId,
        string userIdHash,
        string currency,
        IEnumerable<FinancialScoreRecordProjection> records,
        IEnumerable<FinancialScoreSemanticFactor>? semanticFactors,
        DateTimeOffset calculatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(calculationId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userIdHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentNullException.ThrowIfNull(records);
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
        var income = active
            .Where(item => item.RecordType == FinancialScoreRecordTypes.Income)
            .Sum(item => item.Amount);
        var expense = active
            .Where(item => item.RecordType == FinancialScoreRecordTypes.Expense)
            .Sum(item => item.Amount);

        var cashFlow = CalculateCashFlow(income, expense);
        var monthlyConsistency = CalculateMonthlyConsistency(active);
        var trackingCoverage = Math.Round(
            Math.Min(10m, active.Select(item => item.Date).Distinct().Count() * 10m / TrackingTargetDays),
            2,
            MidpointRounding.AwayFromZero);
        var semantic = CalculateSemanticAdjustment(semanticFactors);
        var raw = FinancialScoreFormula.BaseScore + cashFlow + monthlyConsistency +
            trackingCoverage + semantic;
        var score = (int)Math.Round(
            Math.Clamp(raw, FinancialScoreFormula.Minimum, FinancialScoreFormula.Maximum),
            0,
            MidpointRounding.AwayFromZero);

        var factors = new[]
        {
            new FinancialScoreFactor(
                "cash_flow",
                cashFlow,
                "90-day confirmed income minus expense relative to confirmed income."),
            new FinancialScoreFactor(
                "monthly_consistency",
                monthlyConsistency,
                "Share of observed months with non-negative confirmed cash flow."),
            new FinancialScoreFactor(
                "tracking_coverage",
                trackingCoverage,
                "Distinct days containing confirmed financial records, capped at 30 days."),
            new FinancialScoreFactor(
                "bounded_semantic",
                semantic,
                "Optional bounded semantic adjustments; never an externally supplied final score.")
        };

        return new FinancialScoreCalculation(
            calculationId,
            sourceEventId,
            userIdHash.Trim(),
            currency.Trim().ToUpperInvariant(),
            score,
            FinancialScoreFormula.Version,
            factors,
            calculatedAtUtc.ToUniversalTime());
    }

    private static decimal CalculateCashFlow(decimal income, decimal expense)
    {
        if (income == 0m)
        {
            return expense == 0m ? 0m : -30m;
        }

        return Math.Round(
            Math.Clamp((income - expense) / income, -1m, 1m) * 30m,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static decimal CalculateMonthlyConsistency(
        IReadOnlyCollection<FinancialScoreRecordProjection> records)
    {
        var months = records
            .GroupBy(item => new { item.Date.Year, item.Date.Month })
            .Select(group => new
            {
                Income = group
                    .Where(item => item.RecordType == FinancialScoreRecordTypes.Income)
                    .Sum(item => item.Amount),
                Expense = group
                    .Where(item => item.RecordType == FinancialScoreRecordTypes.Expense)
                    .Sum(item => item.Amount)
            })
            .ToArray();
        if (months.Length == 0)
        {
            return 0m;
        }

        return Math.Round(
            months.Count(month => month.Income >= month.Expense) * 10m / months.Length,
            2,
            MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateSemanticAdjustment(
        IEnumerable<FinancialScoreSemanticFactor>? semanticFactors)
    {
        if (semanticFactors is null)
        {
            return 0m;
        }

        var factors = semanticFactors.ToArray();
        if (factors.Any(item => string.IsNullOrWhiteSpace(item.Code)))
        {
            throw new ArgumentException("Semantic factor codes are required.", nameof(semanticFactors));
        }

        if (factors.Any(item => Math.Abs(item.Adjustment) >
            FinancialScoreFormula.MaximumSemanticFactorAdjustment))
        {
            throw new ArgumentOutOfRangeException(
                nameof(semanticFactors),
                "Each semantic factor adjustment must be between -2 and 2.");
        }

        return Math.Clamp(
            factors.Sum(item => item.Adjustment),
            -FinancialScoreFormula.MaximumSemanticAdjustment,
            FinancialScoreFormula.MaximumSemanticAdjustment);
    }
}
