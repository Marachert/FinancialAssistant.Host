namespace FinancialAssistant.RecommendationsNotifications.Domain;

public static class NotificationTriggerCodes
{
    public const string DailyInputReminder = "daily-input-reminder";
    public const string BudgetApproaching = "budget-limit-approaching";
    public const string BudgetExceeded = "budget-limit-exceeded";
    public const string ScoreImproved = "score-improved";
    public const string RecommendationAvailable = "recommendation-available";
    public const string ReceiptProcessingCompleted = "receipt-processing-completed";
}

public sealed record NotificationTriggerFacts(
    string UserIdHash,
    string Currency,
    DateOnly LocalDate,
    bool HasConfirmedInputToday,
    decimal? BudgetLimit,
    decimal ConfirmedBudgetSpend,
    int? PreviousScore,
    int? CurrentScore,
    bool RecommendationAvailable,
    bool ReceiptProcessingCompleted,
    string SourceEventId,
    string CorrelationId,
    DateTimeOffset OccurredAtUtc);

public sealed record NotificationTriggerCandidate(
    string TriggerId,
    string UserIdHash,
    string Currency,
    string Code,
    string SourceEventId,
    DateTimeOffset OccurredAtUtc);
