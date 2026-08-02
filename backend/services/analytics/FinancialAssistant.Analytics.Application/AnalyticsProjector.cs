using System.Security.Cryptography;
using System.Text;
using FinancialAssistant.Analytics.Domain;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.Analytics.Application;

public sealed class AnalyticsProjector
{
    private const int MaximumTrendDays = 31;
    private readonly IAnalyticsReadModelStore store;
    private readonly IAnalyticsEventPublisher? publisher;
    private readonly IAnalyticsDailyLimitProvider? dailyLimitProvider;

    public AnalyticsProjector(
        IAnalyticsReadModelStore store,
        IAnalyticsEventPublisher? publisher = null,
        IAnalyticsDailyLimitProvider? dailyLimitProvider = null)
    {
        this.store = store;
        this.publisher = publisher;
        this.dailyLimitProvider = dailyLimitProvider;
    }

    public async Task ApplyAsync(
        IntegrationEventEnvelope<FinancialRecordChangedV1> envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var recordType = ReadRecordType(envelope.EventType);
        Validate(envelope);
        var payload = envelope.Payload;

        var outcome = await store.UpsertIfNewerAsync(
            new AnalyticsRecordProjection(
                recordType,
                payload.RecordId,
                envelope.UserIdHash!,
                payload.Amount,
                payload.Currency.ToUpperInvariant(),
                payload.CategoryId,
                payload.Date,
                payload.Status,
                payload.Revision,
                payload.ChangedAtUtc.ToUniversalTime(),
                envelope.EventId),
            cancellationToken);
        if (outcome.PendingPublicationCurrencies.Count == 0)
        {
            return;
        }

        if (publisher is null)
        {
            foreach (var currency in outcome.PendingPublicationCurrencies)
            {
                await store.MarkPublicationCompletedAsync(
                    envelope.EventId,
                    envelope.UserIdHash!,
                    currency,
                    cancellationToken);
            }

            return;
        }

        var reportingDate = DateOnly.FromDateTime(payload.ChangedAtUtc.UtcDateTime);
        foreach (var currency in outcome.PendingPublicationCurrencies)
        {
            await PublishUpdatedAsync(
                envelope,
                currency,
                reportingDate,
                payload.ChangedAtUtc,
                cancellationToken);
            await store.MarkPublicationCompletedAsync(
                envelope.EventId,
                envelope.UserIdHash!,
                currency,
                cancellationToken);
        }
    }

    public async Task RebuildAsync(
        IEnumerable<IntegrationEventEnvelope<FinancialRecordChangedV1>> events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        var ordered = events
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.EventId, StringComparer.Ordinal)
            .ToArray();

        await store.ResetAsync(cancellationToken);
        foreach (var envelope in ordered)
        {
            await ApplyAsync(envelope, cancellationToken);
        }
    }

    public async Task<AnalyticsDashboardReadModel> GetDashboardAsync(
        string userIdHash,
        string currency,
        DateOnly referenceDate,
        decimal? dailyExpenseLimit,
        int trendDays,
        DateTimeOffset asOfUtc,
        TimeSpan staleAfter,
        CancellationToken cancellationToken)
    {
        var normalizedUserIdHash = NormalizeRequired(userIdHash, nameof(userIdHash));
        var normalizedCurrency = NormalizeRequired(currency, nameof(currency)).ToUpperInvariant();
        if (normalizedCurrency.Length != 3)
        {
            throw new ArgumentException("Currency must use a three-letter code.", nameof(currency));
        }

        if (referenceDate == default)
        {
            throw new ArgumentOutOfRangeException(nameof(referenceDate));
        }

        if (dailyExpenseLimit is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dailyExpenseLimit),
                "Daily expense limit must be positive when supplied.");
        }

        if (trendDays is < 1 or > MaximumTrendDays)
        {
            throw new ArgumentOutOfRangeException(
                nameof(trendDays),
                $"Trend days must be between 1 and {MaximumTrendDays}.");
        }

        if (asOfUtc == default || staleAfter <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(asOfUtc));
        }

        var snapshot = await store.GetAsync(
            normalizedUserIdHash,
            normalizedCurrency,
            cancellationToken);
        var monthStart = new DateOnly(referenceDate.Year, referenceDate.Month, 1);
        var daily = snapshot.DailyTotals.GetValueOrDefault(referenceDate) ??
            new AnalyticsAggregateTotals(0m, 0m);
        var monthly = snapshot.MonthlyTotals.GetValueOrDefault(monthStart) ??
            new AnalyticsMonthlyAggregate(
                monthStart,
                new AnalyticsAggregateTotals(0m, 0m),
                Array.Empty<AnalyticsCategoryTotal>());
        var firstTrendDate = referenceDate.AddDays(-(trendDays - 1));
        var trend = Enumerable.Range(0, trendDays)
            .Select(offset => firstTrendDate.AddDays(offset))
            .Select(date => snapshot.DailyTotals.GetValueOrDefault(date) is { } totals
                ? new AnalyticsTrendPoint(date, totals.Income, totals.Expense)
                : new AnalyticsTrendPoint(date, 0m, 0m))
            .ToArray();

        return new AnalyticsDashboardReadModel(
            normalizedUserIdHash,
            normalizedCurrency,
            referenceDate,
            BuildDailyLimit(dailyExpenseLimit, daily.Expense),
            new AnalyticsMonthlyProgress(
                monthly.Totals.Income,
                monthly.Totals.Expense,
                monthly.Totals.BalanceDelta,
                monthly.Totals.Income == 0m
                    ? null
                    : Percentage(monthly.Totals.Expense, monthly.Totals.Income)),
            monthly.CategoryTotals,
            trend,
            snapshot.LastEventAtUtc,
            snapshot.LastEventAtUtc is null ||
                asOfUtc.ToUniversalTime() - snapshot.LastEventAtUtc.Value > staleAfter);
    }

    private static AnalyticsDailyLimit BuildDailyLimit(decimal? limit, decimal spent)
    {
        if (limit is null)
        {
            return new AnalyticsDailyLimit(false, null, spent, null, null);
        }

        return new AnalyticsDailyLimit(
            true,
            limit,
            spent,
            Math.Max(0m, limit.Value - spent),
            Percentage(spent, limit.Value));
    }

    private async Task PublishUpdatedAsync(
        IntegrationEventEnvelope<FinancialRecordChangedV1> source,
        string currency,
        DateOnly referenceDate,
        DateTimeOffset updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var snapshot = await store.GetAsync(
            source.UserIdHash!,
            currency,
            cancellationToken);
        var monthStart = new DateOnly(referenceDate.Year, referenceDate.Month, 1);
        var monthly = snapshot.MonthlyTotals.GetValueOrDefault(monthStart);
        var daily = snapshot.DailyTotals.GetValueOrDefault(referenceDate);
        var topExpenseCategory = monthly?.CategoryTotals
            .Where(item => item.Expense > 0m)
            .OrderByDescending(item => item.Expense)
            .ThenBy(item => item.CategoryId, StringComparer.Ordinal)
            .FirstOrDefault()?.CategoryId;
        var dailyExpenseLimit = dailyLimitProvider is null
            ? null
            : await dailyLimitProvider.GetDailyExpenseLimitAsync(
                source.UserIdHash!,
                currency,
                referenceDate,
                cancellationToken);
        var eventId = StableId("analytics-updated", source.EventId, currency);
        await publisher!.PublishAsync(
            new IntegrationEventEnvelope<AnalyticsUpdatedV1>(
                eventId,
                eventId,
                AnalyticsEventTypes.AnalyticsUpdated,
                updatedAtUtc,
                "financial-assistant-analytics-service",
                AnalyticsEventTypes.SchemaVersion,
                source.CorrelationId,
                source.EventId,
                source.UserIdHash,
                new AnalyticsUpdatedV1(
                    currency,
                    referenceDate,
                    monthly?.Totals.Income ?? 0m,
                    monthly?.Totals.Expense ?? 0m,
                    dailyExpenseLimit,
                    daily?.Expense ?? 0m,
                    topExpenseCategory,
                    updatedAtUtc)),
            cancellationToken);
    }

    private static string StableId(params string[] components)
    {
        var hash = SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('|', components)));
        return Convert.ToHexString(hash).ToLowerInvariant()[..32];
    }

    private static decimal Percentage(decimal numerator, decimal denominator) =>
        Math.Round(numerator / denominator * 100m, 2, MidpointRounding.AwayFromZero);

    private static string ReadRecordType(string eventType)
    {
        if (eventType == FinancialRecordEventTypes.IncomeCreated ||
            eventType == FinancialRecordEventTypes.IncomeUpdated ||
            eventType == FinancialRecordEventTypes.IncomeArchived ||
            eventType == FinancialRecordEventTypes.IncomeRestored)
        {
            return AnalyticsRecordTypes.Income;
        }

        if (eventType == FinancialRecordEventTypes.ExpenseCreated ||
            eventType == FinancialRecordEventTypes.ExpenseUpdated ||
            eventType == FinancialRecordEventTypes.ExpenseArchived ||
            eventType == FinancialRecordEventTypes.ExpenseRestored)
        {
            return AnalyticsRecordTypes.Expense;
        }

        throw new ArgumentException("Unsupported financial record event type.", nameof(eventType));
    }

    private static void Validate(IntegrationEventEnvelope<FinancialRecordChangedV1> envelope)
    {
        var payload = envelope.Payload;
        if (envelope.SchemaVersion != FinancialRecordEventTypes.SchemaVersion ||
            string.IsNullOrWhiteSpace(envelope.UserIdHash) ||
            string.IsNullOrWhiteSpace(payload.RecordId) ||
            payload.Amount <= 0m ||
            string.IsNullOrWhiteSpace(payload.Currency) ||
            payload.Currency.Length != 3 ||
            string.IsNullOrWhiteSpace(payload.CategoryId) ||
            payload.Date == default ||
            payload.Revision < 0 ||
            payload.ChangedAtUtc == default ||
            (payload.Status != AnalyticsProjectionStatuses.Active &&
             payload.Status != AnalyticsProjectionStatuses.Archived))
        {
            throw new ArgumentException("Financial record event is invalid.", nameof(envelope));
        }
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        return value.Trim();
    }
}
