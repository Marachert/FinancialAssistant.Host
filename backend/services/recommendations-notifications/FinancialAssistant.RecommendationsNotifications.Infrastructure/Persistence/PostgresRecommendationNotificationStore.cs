using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.RecommendationsNotifications.Domain;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure.Persistence;

public sealed class PostgresRecommendationNotificationStore(InsightPersistenceContext context) : IRecommendationNotificationStore
{
    public Task<InsightSnapshot?> GetSnapshotAsync(string userIdHash, string currency, CancellationToken cancellationToken)
    {
        InsightPersistenceValidation.Currency(currency);
        return context.ReadAsync<InsightSnapshot?>(userIdHash,
            state => state.Facts.SingleOrDefault(f => f.Currency == currency)?.ToDomain(userIdHash), cancellationToken);
    }

    public Task<InsightApplyResult> ApplyAnalyticsIfNewAsync(string sourceEventId, string userIdHash, string currency,
        AnalyticsInsightFacts analytics, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); InsightPersistenceValidation.Analytics(analytics);
        var source = context.Source;
        if (source.Pipeline != "analytics" || source.Id != sourceEventId || source.Owner != userIdHash || source.Currency != currency ||
            source.Value is not IntegrationEventEnvelope<AnalyticsUpdatedV1> envelope)
        { throw new StorageConflictException(); }
        var p = envelope.Payload;
        if (analytics != new AnalyticsInsightFacts(p.ReferenceDate, p.MonthlyIncomeTotal, p.MonthlyExpenseTotal, p.DailyExpenseLimit,
            p.DailyExpenseSpent, p.TopExpenseCategoryId, p.UpdatedAtUtc, p.TopExpenseCategoryAmount, p.UncategorizedExpenseTotal))
        { throw new StorageConflictException(); }
        var state = context.State(userIdHash);
        var previous = state.Facts.SingleOrDefault(f => f.Currency == currency) ?? new InsightFactsState(currency, null, null, null, null);
        var accepted = source.Order is not null || IsNewer(previous.Analytics?.AtUtc, previous.AnalyticsEvent, analytics.UpdatedAtUtc, sourceEventId);
        if (!accepted) { context.Results([], true); return Task.FromResult(new InsightApplyResult(false, previous.ToDomain(userIdHash))); }
        var updated = previous with { Analytics = InsightAnalyticsState.From(analytics), AnalyticsEvent = sourceEventId };
        state.Facts.Remove(previous); state.Facts.Add(updated); context.Applied();
        return Task.FromResult(new InsightApplyResult(true, updated.ToDomain(userIdHash)));
    }

    public Task<InsightApplyResult> ApplyScoreIfNewAsync(string sourceEventId, string userIdHash, string currency,
        ScoreInsightFacts score, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); InsightPersistenceValidation.Score(score);
        var source = context.Source;
        if (source.Pipeline != "score" || source.Id != sourceEventId || source.Owner != userIdHash || source.Currency != currency ||
            source.Value is not IntegrationEventEnvelope<ScoreCalculatedV1> envelope ||
            score != new ScoreInsightFacts(envelope.Payload.Score, envelope.Payload.FormulaVersion, envelope.Payload.CalculatedAtUtc))
        { throw new StorageConflictException(); }
        var state = context.State(userIdHash);
        var previous = state.Facts.SingleOrDefault(f => f.Currency == currency) ?? new InsightFactsState(currency, null, null, null, null);
        var accepted = source.Order is not null || IsNewer(previous.Score?.AtUtc, previous.ScoreEvent, score.CalculatedAtUtc, sourceEventId);
        if (!accepted) { context.Results([], true); return Task.FromResult(new InsightApplyResult(false, previous.ToDomain(userIdHash))); }
        var updated = previous with { Score = new InsightScoreState(score.Score, score.FormulaVersion, score.CalculatedAtUtc), ScoreEvent = sourceEventId };
        state.Facts.Remove(previous); state.Facts.Add(updated); context.Applied();
        return Task.FromResult(new InsightApplyResult(true, updated.ToDomain(userIdHash)));
    }

    public async Task ReplaceCurrentRecommendationsAsync(string userIdHash, string currency, IReadOnlyList<FinancialRecommendation> recommendations,
        DateTimeOffset changedAtUtc, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = context.Source;
        if (!context.WasApplied || source.Pipeline is not ("analytics" or "score") || source.Owner != userIdHash ||
            source.Currency != currency || source.AtUtc != changedAtUtc || recommendations.Count is < 1 or > 12)
        { throw new StorageConflictException(); }
        var state = context.State(userIdHash);
        var snapshot = state.Facts.Single(f => f.Currency == currency).ToDomain(userIdHash) with
        { Profile = context.Profile ?? throw new StorageUnavailableException() };
        var expected = new RecommendationGenerator().Generate(snapshot, source.Id, source.AtUtc);
        if (expected.Count != recommendations.Count) { throw new StorageConflictException(); }
        foreach (var r in recommendations)
        {
            InsightPersistenceValidation.Recommendation(r);
            var original = expected.SingleOrDefault(e => e.RecommendationId == r.RecommendationId) ?? throw new StorageConflictException();
            if (FactsFingerprint(original) != FactsFingerprint(r) || r.UserIdHash != userIdHash || r.Status != "active" || r.StatusChangedAtUtc != source.AtUtc)
            { throw new StorageConflictException(); }
            await context.ClaimItemAsync("recommendation", r.RecommendationId);
        }
        var ids = recommendations.Select(r => r.RecommendationId).ToArray();
        for (var i = 0; i < state.Recommendations.Count; i++)
        {
            var r = state.Recommendations[i];
            if (r.Currency == currency && !ids.Contains(r.Id, StringComparer.Ordinal) && !RecommendationStatuses.IsTerminal(r.Status))
            { state.Recommendations[i] = r with { Status = "expired", StatusAtUtc = changedAtUtc > r.StatusAtUtc ? changedAtUtc : r.StatusAtUtc }; }
        }
        foreach (var r in recommendations)
        {
            var existing = state.Recommendations.SingleOrDefault(e => e.Id == r.RecommendationId);
            if (existing is not null)
            {
                if (FactsFingerprint(existing.ToDomain(userIdHash)) != FactsFingerprint(r)) { throw new StorageConflictException(); }
                continue;
            }
            state.Recommendations.Add(InsightRecommendationState.From(r));
        }
        context.Results(ids, false);
    }

    public Task MarkInsightEventCompletedAsync(string sourceEventId, string userIdHash, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!context.WasApplied || context.Source.Id != sourceEventId || context.Source.Owner != userIdHash) { throw new StorageConflictException(); }
        context.Complete(); return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FinancialRecommendation>> GetRecommendationsAsync(string userIdHash, string currency, CancellationToken cancellationToken)
    {
        InsightPersistenceValidation.Currency(currency);
        return context.ReadAsync<IReadOnlyList<FinancialRecommendation>>(userIdHash, s => s.Recommendations.Where(r => r.Currency == currency)
            .OrderBy(r => RecommendationStatuses.IsTerminal(r.Status)).ThenByDescending(r => Rank(r.Severity)).ThenByDescending(r => r.GeneratedAtUtc)
            .ThenBy(r => r.Code, StringComparer.Ordinal).Select(r => r.ToDomain(userIdHash)).ToArray(), cancellationToken);
    }

    public Task<FinancialRecommendation?> UpdateRecommendationStatusAsync(string userIdHash, string recommendationId, string status,
        DateTimeOffset changedAtUtc, CancellationToken cancellationToken)
    {
        InsightPersistenceValidation.Id(recommendationId); InsightPersistenceValidation.Utc(changedAtUtc);
        if (status is not ("read" or "dismissed" or "expired")) { throw new ArgumentException("Invalid recommendation transition."); }
        return context.MutateAsync<FinancialRecommendation?>(userIdHash, _ =>
        {
            var state = context.State(userIdHash);
            var existing = state.Recommendations.SingleOrDefault(r => r.Id == recommendationId);
            if (existing is null) { return Task.FromResult<FinancialRecommendation?>(null); }
            if (existing.Status == status) { return Task.FromResult<FinancialRecommendation?>(existing.ToDomain(userIdHash)); }
            if (!RecommendationStatuses.CanTransition(existing.Status, status) || changedAtUtc < existing.StatusAtUtc)
            { throw new InvalidOperationException("Recommendation lifecycle transition is not allowed."); }
            var updated = existing with { Status = status, StatusAtUtc = changedAtUtc };
            state.Recommendations[state.Recommendations.IndexOf(existing)] = updated;
            return Task.FromResult<FinancialRecommendation?>(updated.ToDomain(userIdHash));
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<PreparedNotification>> SaveNotificationsIfNewAsync(IReadOnlyList<PreparedNotification> notifications, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = context.Source;
        var state = context.State();
        var preferences = state.Preferences?.ToDomain() ?? throw new StorageUnavailableException();
        var templates = new NotificationTemplateCatalog();
        IEnumerable<PreparedNotification> expected;
        if (source.Pipeline == "prepare" && source.Value is IntegrationEventEnvelope<RecommendationGeneratedV1> envelope)
        {
            var p = envelope.Payload;
            var r = new FinancialRecommendation(p.RecommendationId, source.Owner, p.Currency, p.Code, p.Severity, p.Title, p.Body,
                p.Facts.Select(f => new RecommendationFact(f.Code, f.Value)).ToArray(), source.Id, p.GeneratedAtUtc, "active", p.GeneratedAtUtc);
            expected = new[] { "push", "web" }.Where(c => preferences.IsEnabled(c, NotificationTriggerCodes.RecommendationAvailable))
                .Select(c => templates.Prepare(r, c, source.Id, source.AtUtc));
        }
        else if (source.Pipeline == "trigger" && source.Value is NotificationTriggerFacts facts)
        {
            expected = new NotificationTriggerEvaluator().Evaluate(facts).SelectMany(t => new[] { "push", "web" }
                .Where(c => preferences.IsEnabled(c, t.Code)).Select(c => templates.Prepare(t, c)));
        }
        else { throw new StorageConflictException(); }
        if (notifications.Count > 12 || InsightPersistenceValidation.Fingerprint(expected.Select(InsightNotificationState.From).ToArray()) !=
            InsightPersistenceValidation.Fingerprint(notifications.Select(InsightNotificationState.From).ToArray()))
        { throw new StorageConflictException(); }
        var accepted = new List<PreparedNotification>();
        foreach (var n in notifications)
        {
            InsightPersistenceValidation.Notification(n);
            if (n.UserIdHash != source.Owner || n.Currency != source.Currency || n.SourceEventId != source.Id || n.DeliveryStatus != "prepared")
            { throw new StorageConflictException(); }
            await context.ClaimItemAsync("notification", n.NotificationId);
            var existing = state.Notifications.SingleOrDefault(e => e.Id == n.NotificationId);
            if (existing is not null)
            {
                // Daily/score trigger identities intentionally span source events; retain the first intent without republishing it.
                if (source.Pipeline == "trigger" && existing.SourceId != source.Id &&
                    existing.RecommendationId == n.RecommendationId && existing.Currency == n.Currency &&
                    existing.Channel == n.Channel && existing.Template == n.TemplateCode && existing.Title == n.Title && existing.Body == n.Body)
                { continue; }
                if (InsightPersistenceValidation.Fingerprint(existing with { Status = "prepared", StatusAtUtc = null, ReadAtUtc = null, Attempt = null }) !=
                    InsightPersistenceValidation.Fingerprint(InsightNotificationState.From(n))) { throw new StorageConflictException(); }
                accepted.Add(existing.ToDomain(source.Owner));
            }
            else { state.Notifications.Add(InsightNotificationState.From(n)); accepted.Add(n); }
        }
        context.Results(accepted.Select(n => n.NotificationId).ToArray(), true);
        return accepted;
    }

    public Task MarkNotificationPublishedAsync(string notificationId, CancellationToken cancellationToken) =>
        throw new StorageUnavailableException();

    public Task<IReadOnlyList<PreparedNotification>> GetNotificationsAsync(string userIdHash, string currency, CancellationToken cancellationToken)
    {
        InsightPersistenceValidation.Currency(currency);
        return context.ReadAsync<IReadOnlyList<PreparedNotification>>(userIdHash, s => s.Notifications.Where(n => n.Currency == currency)
            .OrderByDescending(n => n.PreparedAtUtc).ThenBy(n => n.Id, StringComparer.Ordinal).Select(n => n.ToDomain(userIdHash)).ToArray(), cancellationToken);
    }

    public Task<PreparedNotification?> UpdateNotificationStatusAsync(string userIdHash, string notificationId, string deliveryStatus,
        DateTimeOffset changedAtUtc, CancellationToken cancellationToken)
    {
        InsightPersistenceValidation.Id(notificationId); InsightPersistenceValidation.Utc(changedAtUtc);
        // The gateway cannot assert provider delivery. Real delivery receipts remain an integration gate.
        if (deliveryStatus is not ("failed" or "suppressed")) { throw new InvalidOperationException("Native provider delivery evidence is not qualified."); }
        return context.MutateAsync<PreparedNotification?>(userIdHash, _ =>
        {
            var state = context.State(); var existing = state.Notifications.SingleOrDefault(n => n.Id == notificationId);
            if (existing is null) { return Task.FromResult<PreparedNotification?>(null); }
            if (existing.Status == deliveryStatus) { return Task.FromResult<PreparedNotification?>(existing.ToDomain(userIdHash)); }
            if (existing.Status is not ("prepared" or "retry-scheduled") || changedAtUtc < (existing.StatusAtUtc ?? existing.PreparedAtUtc))
            { throw new InvalidOperationException("Notification lifecycle transition is not allowed."); }
            var updated = existing with { Status = deliveryStatus, StatusAtUtc = changedAtUtc };
            state.Notifications[state.Notifications.IndexOf(existing)] = updated;
            return Task.FromResult<PreparedNotification?>(updated.ToDomain(userIdHash));
        }, cancellationToken);
    }

    public Task<PreparedNotification?> MarkNotificationReadAsync(string userIdHash, string notificationId, DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken)
    {
        InsightPersistenceValidation.Id(notificationId); InsightPersistenceValidation.Utc(changedAtUtc);
        return context.MutateAsync<PreparedNotification?>(userIdHash, _ =>
        {
            var state = context.State(); var existing = state.Notifications.SingleOrDefault(n => n.Id == notificationId);
            if (existing is null) { return Task.FromResult<PreparedNotification?>(null); }
            if (existing.ReadAtUtc is not null) { return Task.FromResult<PreparedNotification?>(existing.ToDomain(userIdHash)); }
            if (changedAtUtc < existing.PreparedAtUtc) { throw new InvalidOperationException("Notification read time precedes preparation."); }
            var updated = existing with { ReadAtUtc = changedAtUtc };
            state.Notifications[state.Notifications.IndexOf(existing)] = updated;
            return Task.FromResult<PreparedNotification?>(updated.ToDomain(userIdHash));
        }, cancellationToken);
    }

    public Task<PreparedNotification?> RecordUndeliveredAttemptAsync(string owner, NotificationDeliveryAttempt attempt, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(attempt); InsightPersistenceValidation.Id(attempt.NotificationId); InsightPersistenceValidation.Utc(attempt.AttemptedAtUtc);
        if (attempt.AttemptNumber is < 1 or > 10000 || attempt.Status is not ("failed" or "suppressed" or "retry-scheduled") ||
            attempt.FailureCode is not ("channel-disabled" or "provider-not-configured" or "provider-adapter-placeholder" or "provider-unavailable") ||
            attempt.IsRetryable != (attempt.Status == "retry-scheduled") || attempt.IsRetryable != (attempt.RetryAtUtc is not null))
        { throw new ArgumentException("Only explicit undelivered attempt evidence is supported."); }
        if (attempt.RetryAtUtc is { } retry)
        { InsightPersistenceValidation.Utc(retry); if (retry <= attempt.AttemptedAtUtc) { throw new ArgumentException("Invalid notification retry time."); } }
        return context.MutateAsync<PreparedNotification?>(owner, _ =>
        {
            var state = context.State(); var existing = state.Notifications.SingleOrDefault(n => n.Id == attempt.NotificationId);
            if (existing is null) { return Task.FromResult<PreparedNotification?>(null); }
            var value = new InsightAttemptState(attempt.AttemptNumber, attempt.Status, attempt.FailureCode!, attempt.AttemptedAtUtc, attempt.RetryAtUtc);
            if (existing.Attempt is { } previous && previous.Number == value.Number)
            {
                if (previous != value || attempt.Channel != existing.Channel) { throw new StorageConflictException(); }
                return Task.FromResult<PreparedNotification?>(existing.ToDomain(owner));
            }
            if (attempt.Channel != existing.Channel || existing.Status is not ("prepared" or "retry-scheduled") ||
                value.Number != (existing.Attempt?.Number ?? 0) + 1 || value.AtUtc < (existing.StatusAtUtc ?? existing.PreparedAtUtc))
            { throw new StorageConflictException(); }
            var updated = existing with { Status = value.Status, StatusAtUtc = value.AtUtc, Attempt = value };
            state.Notifications[state.Notifications.IndexOf(existing)] = updated;
            return Task.FromResult<PreparedNotification?>(updated.ToDomain(owner));
        }, token);
    }

    private static bool IsNewer(DateTimeOffset? previous, string? previousId, DateTimeOffset at, string id) =>
        previous is null || at > previous || at == previous && string.CompareOrdinal(id, previousId) > 0;
    private static int Rank(string severity) => severity == "critical" ? 3 : severity == "warning" ? 2 : 1;
    private static string FactsFingerprint(FinancialRecommendation r) => InsightPersistenceValidation.Fingerprint(new
    {
        r.RecommendationId,
        r.UserIdHash,
        r.Currency,
        r.Code,
        r.Severity,
        r.SourceEventId,
        r.GeneratedAtUtc,
        Facts = r.Facts.Select(f => new InsightFactState(f.Code, OwnedRecordCodec.CanonicalDecimal(f.Value))).ToArray()
    });
}
