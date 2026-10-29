using System.Security.Cryptography;
using System.Text.Json.Serialization;
using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure.Persistence;

internal sealed record InsightState([property: JsonRequired] string Owner,
    [property: JsonRequired] List<InsightFactsState> Facts,
    [property: JsonRequired] List<InsightReceiptState> Receipts,
    [property: JsonRequired] List<InsightRecommendationState> Recommendations,
    [property: JsonRequired] List<InsightNotificationState> Notifications,
    [property: JsonRequired] List<InsightPublicationState> Publications,
    [property: JsonRequired] InsightPreferenceState? Preferences)
{
    public static InsightState Empty(string owner) => new(owner, [], [], [], [], [], null);
}

internal sealed record InsightAnalyticsState([property: JsonRequired] DateOnly Date,
    [property: JsonRequired] decimal Income, [property: JsonRequired] decimal Expense,
    [property: JsonRequired] decimal? DailyLimit, [property: JsonRequired] decimal DailySpent,
    [property: JsonRequired] string? Category, [property: JsonRequired] DateTimeOffset AtUtc,
    [property: JsonRequired] decimal CategoryAmount, [property: JsonRequired] decimal Uncategorized)
{
    public AnalyticsInsightFacts ToDomain() => new(Date, Income, Expense, DailyLimit, DailySpent, Category, AtUtc, CategoryAmount, Uncategorized);
    public static InsightAnalyticsState From(AnalyticsInsightFacts f) => new(f.ReferenceDate, f.MonthlyIncomeTotal,
        f.MonthlyExpenseTotal, f.DailyExpenseLimit, f.DailyExpenseSpent, f.TopExpenseCategoryId, f.UpdatedAtUtc,
        f.TopExpenseCategoryAmount, f.UncategorizedExpenseTotal);
}
internal sealed record InsightScoreState([property: JsonRequired] int Score, [property: JsonRequired] string Formula,
    [property: JsonRequired] DateTimeOffset AtUtc);
internal sealed record InsightFactsState([property: JsonRequired] string Currency,
    [property: JsonRequired] InsightAnalyticsState? Analytics, [property: JsonRequired] string? AnalyticsEvent,
    [property: JsonRequired] InsightScoreState? Score, [property: JsonRequired] string? ScoreEvent)
{
    public InsightSnapshot ToDomain(string owner) => new(owner, Currency, Analytics?.ToDomain(),
        Score is null ? null : new ScoreInsightFacts(Score.Score, Score.Formula, Score.AtUtc));
}
internal sealed record InsightReceiptState([property: JsonRequired] string Pipeline,
    [property: JsonRequired] string SourceId, [property: JsonRequired] string Fingerprint,
    [property: JsonRequired] string[] Results);
internal sealed record InsightFactState([property: JsonRequired] string Code, [property: JsonRequired] decimal Value);
internal sealed record InsightExplanationState([property: JsonRequired] string LocalizationKey,
    [property: JsonRequired] string Text, [property: JsonRequired] string Confidence,
    [property: JsonRequired] string ActionCode, [property: JsonRequired] string ActionRoute,
    [property: JsonRequired] bool Enhanced);
internal sealed record InsightRecommendationState([property: JsonRequired] string Id,
    [property: JsonRequired] string Currency, [property: JsonRequired] string Code,
    [property: JsonRequired] string Severity, [property: JsonRequired] string Title,
    [property: JsonRequired] string Body, [property: JsonRequired] InsightFactState[] Facts,
    [property: JsonRequired] string SourceId, [property: JsonRequired] DateTimeOffset GeneratedAtUtc,
    [property: JsonRequired] string Status, [property: JsonRequired] DateTimeOffset StatusAtUtc,
    [property: JsonRequired] InsightExplanationState? Explanation)
{
    public FinancialRecommendation ToDomain(string owner) => new(Id, owner, Currency, Code, Severity, Title, Body,
        Facts.Select(f => new RecommendationFact(f.Code, f.Value)).ToArray(), SourceId, GeneratedAtUtc, Status, StatusAtUtc,
        Explanation is null ? null : new RecommendationExplanation(Explanation.LocalizationKey, Explanation.Text,
            Explanation.Confidence, new RecommendationActionLink(Explanation.ActionCode, Explanation.ActionRoute), Explanation.Enhanced));
    public static InsightRecommendationState From(FinancialRecommendation r) => new(r.RecommendationId, r.Currency,
        r.Code, r.Severity, r.Title, r.Body, r.Facts.Select(f => new InsightFactState(f.Code, f.Value)).ToArray(),
        r.SourceEventId, r.GeneratedAtUtc, r.Status, r.StatusChangedAtUtc, r.Explanation is null ? null :
            new InsightExplanationState(r.Explanation.LocalizationKey, r.Explanation.Text, r.Explanation.Confidence,
                r.Explanation.Action.Code, r.Explanation.Action.Route, r.Explanation.IsWordingEnhanced));
}
internal sealed record InsightAttemptState([property: JsonRequired] int Number, [property: JsonRequired] string Status,
    [property: JsonRequired] string Failure, [property: JsonRequired] DateTimeOffset AtUtc,
    [property: JsonRequired] DateTimeOffset? RetryAtUtc);
internal sealed record InsightNotificationState([property: JsonRequired] string Id,
    [property: JsonRequired] string RecommendationId, [property: JsonRequired] string Currency,
    [property: JsonRequired] string Channel, [property: JsonRequired] string Template,
    [property: JsonRequired] string Title, [property: JsonRequired] string Body,
    [property: JsonRequired] string Status, [property: JsonRequired] string SourceId,
    [property: JsonRequired] DateTimeOffset PreparedAtUtc, [property: JsonRequired] DateTimeOffset? StatusAtUtc,
    [property: JsonRequired] DateTimeOffset? ReadAtUtc, [property: JsonRequired] InsightAttemptState? Attempt)
{
    public PreparedNotification ToDomain(string owner) => new(Id, RecommendationId, owner, Currency, Channel,
        Template, Title, Body, Status, SourceId, PreparedAtUtc, StatusAtUtc, ReadAtUtc);
    public static InsightNotificationState From(PreparedNotification n) => new(n.NotificationId, n.RecommendationId,
        n.Currency, n.Channel, n.TemplateCode, n.Title, n.Body, n.DeliveryStatus, n.SourceEventId, n.PreparedAtUtc, n.StatusChangedAtUtc, n.ReadAtUtc, null);
}
internal sealed record InsightPreferenceState([property: JsonRequired] bool Push, [property: JsonRequired] bool Web,
    [property: JsonRequired] TimeOnly? Starts, [property: JsonRequired] TimeOnly? Ends,
    [property: JsonRequired] string? Zone, [property: JsonRequired] string[] Disabled)
{
    public NotificationPreferences ToDomain() => new(Push, Web, Starts is null ? null : new NotificationQuietHours(Starts.Value, Ends!.Value, Zone!), Disabled.ToArray());
    public static InsightPreferenceState From(NotificationPreferences p) => new(p.PushEnabled, p.WebEnabled,
        p.QuietHours?.StartsAt, p.QuietHours?.EndsAt, p.QuietHours?.TimeZoneId,
        (p.DisabledNotificationTypes ?? []).Order(StringComparer.Ordinal).ToArray());
}
internal sealed record InsightPublicationState([property: JsonRequired] string Kind, [property: JsonRequired] string ItemId,
    [property: JsonRequired] string OccurrenceId, [property: JsonRequired] string CorrelationId,
    [property: JsonRequired] string CausationId, [property: JsonRequired] DateTimeOffset? PublishedAtUtc);

public static class InsightPersistenceValidation
{
    public static void Owner(string value)
    {
        if (value is not { Length: 64 } || value.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
        { throw new ArgumentException("Invalid insight owner hash."); }
    }
    public static void Id(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value != value.Trim() ||
            value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or '~')))
        { throw new ArgumentException("Invalid insight identifier."); }
    }
    public static void Currency(string value)
    {
        if (value is not { Length: 3 } || value.Any(c => c is < 'A' or > 'Z')) { throw new ArgumentException("Invalid insight currency."); }
    }
    public static void Utc(DateTimeOffset value)
    { if (value == default || value.Offset != TimeSpan.Zero) { throw new ArgumentException("An insight UTC timestamp is required."); } }
    public static void Text(string value, int maximum)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value != value.Trim() || value.Any(char.IsControl))
        { throw new ArgumentException("Invalid safe insight text."); }
    }
    public static void Preferences(NotificationPreferences p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (p.DisabledNotificationTypes?.Any(t => !NotificationTriggerCodes.IsKnown(t)) == true ||
            p.DisabledNotificationTypes is { Count: > 6 } || p.DisabledNotificationTypes?.Distinct().Count() != p.DisabledNotificationTypes?.Count)
        { throw new ArgumentException("Invalid disabled notification types."); }
        if (p.QuietHours is { } q)
        {
            Text(q.TimeZoneId, 100);
            if (q.StartsAt == q.EndsAt || !TimeZoneInfo.TryFindSystemTimeZoneById(q.TimeZoneId, out _))
            { throw new ArgumentException("Invalid notification quiet hours."); }
        }
    }
    internal static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(OwnedRecordCodec.Encode(1, value)));
    internal static byte[] Encode(InsightState state)
    {
        Validate(state);
        var bytes = OwnedRecordCodec.Encode(1, state);
        if (bytes.Length > 900_000) { throw new StorageUnavailableException(); }
        return bytes;
    }
    internal static InsightState Decode(DurableRecord record, string owner)
    {
        try
        {
            var state = OwnedRecordCodec.Decode<InsightState>(1, record);
            Validate(state);
            if (state.Owner != owner || record.Payload.Length > 900_000) { throw new StorageUnavailableException(); }
            return state;
        }
        catch (Exception e) when (e is ArgumentException or NullReferenceException or InvalidOperationException)
        { throw new StorageUnavailableException(); }
    }
    private static void Validate(InsightState s)
    {
        Owner(s.Owner);
        if (s.Facts is null || s.Receipts is null || s.Recommendations is null || s.Notifications is null || s.Publications is null ||
            s.Facts.Count > 32 || s.Receipts.Count > 1000 || s.Recommendations.Count > 1000 || s.Notifications.Count > 1000 || s.Publications.Count > 2000 ||
            s.Facts.DistinctBy(f => f.Currency).Count() != s.Facts.Count || s.Receipts.DistinctBy(r => (r.Pipeline, r.SourceId)).Count() != s.Receipts.Count ||
            s.Recommendations.DistinctBy(r => r.Id).Count() != s.Recommendations.Count || s.Notifications.DistinctBy(n => n.Id).Count() != s.Notifications.Count ||
            s.Publications.DistinctBy(p => (p.Kind, p.ItemId)).Count() != s.Publications.Count)
        { throw new StorageUnavailableException(); }
        foreach (var f in s.Facts)
        {
            Currency(f.Currency);
            if ((f.Analytics is null) != (f.AnalyticsEvent is null) || (f.Score is null) != (f.ScoreEvent is null)) { throw new StorageUnavailableException(); }
            if (f.Analytics is { } a) { Analytics(a.ToDomain()); Id(f.AnalyticsEvent!); }
            if (f.Score is { } c) { Score(new ScoreInsightFacts(c.Score, c.Formula, c.AtUtc)); Id(f.ScoreEvent!); }
        }
        foreach (var r in s.Receipts)
        {
            Id(r.SourceId);
            if (r.Pipeline is not ("analytics" or "score" or "prepare" or "trigger") || r.Results is null || r.Results.Length > 12 ||
                r.Fingerprint is not { Length: 64 } || !r.Fingerprint.All(char.IsAsciiHexDigit)) { throw new StorageUnavailableException(); }
            foreach (var id in r.Results) { Id(id); }
        }
        foreach (var r in s.Recommendations) { Recommendation(r.ToDomain(s.Owner)); }
        foreach (var n in s.Notifications)
        {
            Notification(n.ToDomain(s.Owner));
            if (n.Attempt is { } a)
            {
                Utc(a.AtUtc);
                if (a.Number < 1 || a.Status is not ("failed" or "suppressed" or "retry-scheduled") ||
                    n.StatusAtUtc is null || a.AtUtc > n.StatusAtUtc || a.AtUtc < n.PreparedAtUtc ||
                    a.Failure is not ("channel-disabled" or "provider-not-configured" or "provider-adapter-placeholder" or "provider-unavailable") ||
                    (a.Status == "retry-scheduled") != (a.RetryAtUtc is not null))
                { throw new StorageUnavailableException(); }
                if (a.RetryAtUtc is { } retry) { Utc(retry); if (retry <= a.AtUtc) { throw new StorageUnavailableException(); } }
            }
        }
        if (s.Preferences is { } preferences)
        {
            if ((preferences.Starts is null) != (preferences.Ends is null) || (preferences.Starts is null) != (preferences.Zone is null) || preferences.Disabled is null)
            { throw new StorageUnavailableException(); }
            Preferences(preferences.ToDomain());
        }
        foreach (var p in s.Publications)
        {
            Id(p.ItemId); Id(p.OccurrenceId); Id(p.CorrelationId); Id(p.CausationId);
            var at = p.Kind == "recommendation" ? s.Recommendations.Single(r => r.Id == p.ItemId).GeneratedAtUtc :
                p.Kind == "notification" ? s.Notifications.Single(n => n.Id == p.ItemId).PreparedAtUtc : throw new StorageUnavailableException();
            if (p.PublishedAtUtc is { } published) { Utc(published); if (published < at) { throw new StorageUnavailableException(); } }
        }
        foreach (var r in s.Receipts)
        {
            var kind = r.Pipeline is "analytics" or "score" ? "recommendation" : "notification";
            if (r.Results.Distinct().Count() != r.Results.Length || r.Results.Any(id => !s.Publications.Any(p => p.Kind == kind && p.ItemId == id && p.CausationId == r.SourceId)))
            { throw new StorageUnavailableException(); }
        }
        if (s.Recommendations.Any(r => !s.Publications.Any(p => p.Kind == "recommendation" && p.ItemId == r.Id && p.CausationId == r.SourceId)) ||
            s.Notifications.Any(n => !s.Publications.Any(p => p.Kind == "notification" && p.ItemId == n.Id && p.CausationId == n.SourceId)))
        { throw new StorageUnavailableException(); }
    }
    public static void Analytics(AnalyticsInsightFacts a)
    {
        ArgumentNullException.ThrowIfNull(a); Utc(a.UpdatedAtUtc);
        if (a.ReferenceDate == default || a.MonthlyIncomeTotal < 0 || a.MonthlyExpenseTotal < 0 || a.DailyExpenseLimit is <= 0 ||
            a.DailyExpenseSpent < 0 || a.TopExpenseCategoryAmount < 0 || a.UncategorizedExpenseTotal < 0 ||
            a.DailyExpenseSpent > a.MonthlyExpenseTotal || a.TopExpenseCategoryAmount > a.MonthlyExpenseTotal || a.UncategorizedExpenseTotal > a.MonthlyExpenseTotal)
        { throw new ArgumentException("Invalid deterministic Analytics facts."); }
        if (a.TopExpenseCategoryId is not null) { Id(a.TopExpenseCategoryId); }
    }
    public static void Score(ScoreInsightFacts s)
    {
        ArgumentNullException.ThrowIfNull(s); Utc(s.CalculatedAtUtc);
        if (s.Score is < 0 or > 100 || s.FormulaVersion != "financial-score-v2") { throw new ArgumentException("Invalid deterministic Score facts."); }
    }
    public static void Recommendation(FinancialRecommendation r)
    {
        Owner(r.UserIdHash); Currency(r.Currency); Id(r.RecommendationId); Id(r.SourceEventId); Id(r.Code);
        Text(r.Title, 120); Text(r.Body, 500); Utc(r.GeneratedAtUtc); Utc(r.StatusChangedAtUtc);
        if (r.Severity is not ("information" or "warning" or "critical") || r.Status is not ("active" or "read" or "dismissed" or "expired") ||
            r.StatusChangedAtUtc < r.GeneratedAtUtc || r.Facts is null || r.Facts.Count > 12)
        { throw new ArgumentException("Invalid recommendation state."); }
        foreach (var f in r.Facts) { Id(f.Code); }
        if (r.Explanation is { } e)
        {
            Text(e.Text, 500); Id(e.LocalizationKey); Id(e.Action.Code);
            var expected = RecommendationExplanationCatalog.CreateInput(r);
            if (!RecommendationExplanationConfidences.IsKnown(e.Confidence) || e.Action != expected.Action || e.LocalizationKey != expected.LocalizationKey)
            { throw new ArgumentException("Invalid safe recommendation explanation."); }
        }
    }
    public static void Notification(PreparedNotification n)
    {
        Owner(n.UserIdHash); Currency(n.Currency); Id(n.NotificationId); Id(n.RecommendationId); Id(n.SourceEventId); Id(n.TemplateCode);
        Text(n.Title, 120); Text(n.Body, 500); Utc(n.PreparedAtUtc);
        if (n.Channel is not ("push" or "web") || !NotificationDeliveryStatuses.IsKnown(n.DeliveryStatus))
        { throw new ArgumentException("Invalid prepared notification state."); }
        foreach (var at in new[] { n.StatusChangedAtUtc, n.ReadAtUtc })
        { if (at is { } t) { Utc(t); if (t < n.PreparedAtUtc) { throw new ArgumentException("Notification lifecycle time precedes preparation."); } } }
    }
}
