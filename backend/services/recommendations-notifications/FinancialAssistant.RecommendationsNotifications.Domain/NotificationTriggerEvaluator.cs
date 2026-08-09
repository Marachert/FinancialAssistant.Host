using System.Globalization;

namespace FinancialAssistant.RecommendationsNotifications.Domain;

public sealed class NotificationTriggerEvaluator
{
    private const decimal BudgetApproachingPercent = 80m;

    public IReadOnlyList<NotificationTriggerCandidate> Evaluate(
        NotificationTriggerFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var owner = NormalizeRequired(facts.UserIdHash);
        var currency = NormalizeCurrency(facts.Currency);
        var sourceEventId = NormalizeRequired(facts.SourceEventId);
        _ = NormalizeRequired(facts.CorrelationId);
        if (facts.LocalDate == default ||
            facts.OccurredAtUtc == default ||
            facts.ConfirmedBudgetSpend < 0m ||
            facts.PreviousScore is < 0 or > 100 ||
            facts.CurrentScore is < 0 or > 100)
        {
            throw new ArgumentException("Notification trigger facts are invalid.", nameof(facts));
        }

        var codes = new List<string>();
        if (!facts.HasConfirmedInputToday)
        {
            codes.Add(NotificationTriggerCodes.DailyInputReminder);
        }

        if (facts.BudgetLimit is > 0m)
        {
            var usedPercent = facts.ConfirmedBudgetSpend / facts.BudgetLimit.Value * 100m;
            codes.Add(
                usedPercent >= 100m
                    ? NotificationTriggerCodes.BudgetExceeded
                    : usedPercent >= BudgetApproachingPercent
                        ? NotificationTriggerCodes.BudgetApproaching
                        : string.Empty);
        }

        if (facts.PreviousScore.HasValue &&
            facts.CurrentScore.HasValue &&
            facts.CurrentScore.Value > facts.PreviousScore.Value)
        {
            codes.Add(NotificationTriggerCodes.ScoreImproved);
        }

        if (facts.RecommendationAvailable)
        {
            codes.Add(NotificationTriggerCodes.RecommendationAvailable);
        }

        if (facts.ReceiptProcessingCompleted)
        {
            codes.Add(NotificationTriggerCodes.ReceiptProcessingCompleted);
        }

        return codes
            .Where(code => code.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Select(code => new NotificationTriggerCandidate(
                RecommendationGenerator.StableId(
                    "notification-trigger",
                    owner,
                    currency,
                    code,
                    DeduplicationKey(code, facts)),
                owner,
                currency,
                code,
                sourceEventId,
                facts.OccurredAtUtc.ToUniversalTime()))
            .ToArray();
    }

    private static string DeduplicationKey(
        string code,
        NotificationTriggerFacts facts) =>
        code switch
        {
            NotificationTriggerCodes.DailyInputReminder or
            NotificationTriggerCodes.BudgetApproaching or
            NotificationTriggerCodes.BudgetExceeded =>
                facts.LocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            NotificationTriggerCodes.ScoreImproved =>
                string.Join(
                    ':',
                    facts.LocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    facts.CurrentScore!.Value.ToString(CultureInfo.InvariantCulture)),
            _ => facts.SourceEventId.Trim()
        };

    private static string NormalizeCurrency(string value)
    {
        var currency = NormalizeRequired(value).ToUpperInvariant();
        return currency.Length == 3
            ? currency
            : throw new ArgumentException("Currency must use a three-letter code.", nameof(value));
    }

    private static string NormalizeRequired(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", nameof(value))
            : value.Trim();
}
