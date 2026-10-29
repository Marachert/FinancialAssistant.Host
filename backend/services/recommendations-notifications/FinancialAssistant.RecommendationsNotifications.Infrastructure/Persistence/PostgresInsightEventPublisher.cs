using FinancialAssistant.RecommendationsNotifications.Application;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.RecommendationsNotifications.Infrastructure.Persistence;

public sealed record RecommendationOutboxMessage(IntegrationEventEnvelope<RecommendationGeneratedV1> Event, DateTimeOffset? PublishedAtUtc);
public sealed record NotificationOutboxMessage(IntegrationEventEnvelope<NotificationPreparedV1> Event, DateTimeOffset? PublishedAtUtc);

public sealed class PostgresInsightEventPublisher(InsightPersistenceContext context) : IRecommendationEventPublisher, INotificationEventPublisher
{
    public bool ConfirmsDelivery => false;
    public Task PublishAsync(IntegrationEventEnvelope<RecommendationGeneratedV1> envelope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = context.Source; var state = context.State(envelope.UserIdHash);
        if (source.Pipeline is not ("analytics" or "score")) { throw new StorageConflictException(); }
        var r = state.Recommendations.SingleOrDefault(r => r.Id == envelope.Payload.RecommendationId) ?? throw new StorageUnavailableException();
        var p = new InsightPublicationState("recommendation", r.Id, r.Id, source.Correlation, source.Id, null);
        if (InsightPersistenceValidation.Fingerprint(MapRecommendation(state.Owner, r, p)) != InsightPersistenceValidation.Fingerprint(envelope))
        { throw new StorageConflictException(); }
        Stage(state, p); return Task.CompletedTask;
    }
    public Task PublishAsync(IntegrationEventEnvelope<NotificationPreparedV1> envelope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = context.Source; var state = context.State(envelope.UserIdHash);
        if (source.Pipeline is not ("prepare" or "trigger")) { throw new StorageConflictException(); }
        var n = state.Notifications.SingleOrDefault(n => n.Id == envelope.Payload.NotificationId) ?? throw new StorageUnavailableException();
        var p = new InsightPublicationState("notification", n.Id, n.Id, source.Correlation, source.Id, null);
        if (InsightPersistenceValidation.Fingerprint(MapNotification(state.Owner, n, p)) != InsightPersistenceValidation.Fingerprint(envelope))
        { throw new StorageConflictException(); }
        Stage(state, p); return Task.CompletedTask;
    }
    private static void Stage(InsightState state, InsightPublicationState p)
    {
        var previous = state.Publications.SingleOrDefault(x => x.Kind == p.Kind && x.ItemId == p.ItemId);
        if (previous is null) { state.Publications.Add(p); }
        else if (previous with { PublishedAtUtc = null } != p) { throw new StorageConflictException(); }
    }
    public Task<IReadOnlyList<RecommendationOutboxMessage>> ReadRecommendationsAsync(string owner, CancellationToken token = default) =>
        context.ReadAsync<IReadOnlyList<RecommendationOutboxMessage>>(owner, s => s.Publications.Where(p => p.Kind == "recommendation" && p.PublishedAtUtc is null)
            .Take(25).Select(p => new RecommendationOutboxMessage(MapRecommendation(owner, s.Recommendations.Single(r => r.Id == p.ItemId), p), null)).ToArray(), token);
    public Task<IReadOnlyList<NotificationOutboxMessage>> ReadNotificationsAsync(string owner, CancellationToken token = default) =>
        context.ReadAsync<IReadOnlyList<NotificationOutboxMessage>>(owner, s => s.Publications.Where(p => p.Kind == "notification" && p.PublishedAtUtc is null)
            .Take(25).Select(p => new NotificationOutboxMessage(MapNotification(owner, s.Notifications.Single(n => n.Id == p.ItemId), p), null)).ToArray(), token);

    public Task<bool> MarkPublishedAsync(RecommendationOutboxMessage expected, DateTimeOffset atUtc, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(expected); ArgumentNullException.ThrowIfNull(expected.Event);
        return Acknowledge(expected.Event.UserIdHash!, "recommendation", expected.Event.Payload.RecommendationId, atUtc,
            (s, p) => InsightPersistenceValidation.Fingerprint(MapRecommendation(s.Owner, s.Recommendations.Single(r => r.Id == p.ItemId), p)) == InsightPersistenceValidation.Fingerprint(expected.Event), token);
    }
    public Task<bool> MarkPublishedAsync(NotificationOutboxMessage expected, DateTimeOffset atUtc, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(expected); ArgumentNullException.ThrowIfNull(expected.Event);
        return Acknowledge(expected.Event.UserIdHash!, "notification", expected.Event.Payload.NotificationId, atUtc,
            (s, p) => InsightPersistenceValidation.Fingerprint(MapNotification(s.Owner, s.Notifications.Single(n => n.Id == p.ItemId), p)) == InsightPersistenceValidation.Fingerprint(expected.Event), token);
    }
    private Task<bool> Acknowledge(string owner, string kind, string id, DateTimeOffset atUtc, Func<InsightState, InsightPublicationState, bool> matches, CancellationToken token)
    {
        InsightPersistenceValidation.Id(id); InsightPersistenceValidation.Utc(atUtc);
        return context.MutateAsync(owner, _ =>
        {
            var s = context.State(); var p = s.Publications.SingleOrDefault(p => p.Kind == kind && p.ItemId == id) ?? throw new StorageUnavailableException();
            var occurred = kind == "recommendation" ? s.Recommendations.Single(r => r.Id == id).GeneratedAtUtc : s.Notifications.Single(n => n.Id == id).PreparedAtUtc;
            if (atUtc < occurred) { throw new ArgumentException("Insight acknowledgement precedes its intent."); }
            if (!matches(s, p)) { throw new StorageConflictException(); }
            if (p.PublishedAtUtc is not null) { return Task.FromResult(false); }
            s.Publications[s.Publications.IndexOf(p)] = p with { PublishedAtUtc = atUtc }; return Task.FromResult(true);
        }, token);
    }
    internal static IntegrationEventEnvelope<RecommendationGeneratedV1> MapRecommendation(string owner, InsightRecommendationState r, InsightPublicationState p) =>
        new("recommendation-" + r.Id, p.OccurrenceId, RecommendationEventTypes.RecommendationGenerated, r.GeneratedAtUtc,
            "financial-assistant-recommendations-service", 1, p.CorrelationId, p.CausationId, owner,
            new RecommendationGeneratedV1(r.Id, r.Currency, r.Code, r.Severity, r.Title, r.Body,
                r.Facts.Select(f => new RecommendationFactV1(f.Code, f.Value)).ToArray(), r.GeneratedAtUtc));
    internal static IntegrationEventEnvelope<NotificationPreparedV1> MapNotification(string owner, InsightNotificationState n, InsightPublicationState p) =>
        new("notification-" + n.Id, p.OccurrenceId, NotificationEventTypes.NotificationPrepared, n.PreparedAtUtc,
            "financial-assistant-notification-service", 1, p.CorrelationId, p.CausationId, owner,
            new NotificationPreparedV1(n.Id, n.RecommendationId, n.Currency, n.Channel, n.Template, "prepared", n.PreparedAtUtc));
}
