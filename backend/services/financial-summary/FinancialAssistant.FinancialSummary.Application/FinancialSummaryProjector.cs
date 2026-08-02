using FinancialAssistant.FinancialSummary.Domain;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.FinancialSummary.Application;

public sealed class FinancialSummaryProjector
{
    private readonly IFinancialSummaryReadModelStore store;

    public FinancialSummaryProjector(IFinancialSummaryReadModelStore store)
    {
        this.store = store;
    }

    public async Task ApplyAsync(
        IntegrationEventEnvelope<FinancialRecordChangedV1> envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var recordType = ReadRecordType(envelope.EventType);
        var payload = envelope.Payload;
        Validate(envelope, payload);

        await store.UpsertIfNewerAsync(
            new FinancialRecordProjection(
                recordType,
                payload.RecordId,
                envelope.UserIdHash!,
                payload.Amount,
                payload.Currency,
                payload.CategoryId,
                payload.Date,
                payload.Status,
                payload.Revision,
                payload.Origin,
                payload.ChangedAtUtc.ToUniversalTime(),
                envelope.EventId),
            cancellationToken);
    }

    public async Task RebuildAsync(
        IEnumerable<IntegrationEventEnvelope<FinancialRecordChangedV1>> events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);
        var orderedEvents = events
            .OrderBy(envelope => envelope.OccurredAtUtc)
            .ThenBy(envelope => envelope.EventId, StringComparer.Ordinal)
            .ToArray();

        await store.ResetAsync(cancellationToken);
        foreach (var envelope in orderedEvents)
        {
            await ApplyAsync(envelope, cancellationToken);
        }
    }

    public async Task<FinancialSummaryReadModel> GetAsync(
        string userIdHash,
        string currency,
        DateOnly referenceDate,
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

        if (asOfUtc == default)
        {
            throw new ArgumentOutOfRangeException(nameof(asOfUtc));
        }

        if (staleAfter <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(staleAfter));
        }

        var records = await store.ListAsync(
            normalizedUserIdHash,
            normalizedCurrency,
            cancellationToken);
        var activeRecords = records
            .Where(record => record.Status == FinancialRecordProjectionStatuses.Active)
            .ToArray();

        var day = BuildTotals(activeRecords, referenceDate, referenceDate);
        var weekStart = referenceDate.AddDays(-(((int)referenceDate.DayOfWeek + 6) % 7));
        var week = BuildTotals(activeRecords, weekStart, weekStart.AddDays(6));
        var monthStart = new DateOnly(referenceDate.Year, referenceDate.Month, 1);
        var month = BuildTotals(
            activeRecords,
            monthStart,
            monthStart.AddMonths(1).AddDays(-1));
        var categories = activeRecords
            .Where(record => record.Date >= month.From && record.Date <= month.To)
            .GroupBy(record => record.CategoryId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new FinancialCategoryTotals(
                group.Key,
                Sum(group, FinancialRecordTypes.Income),
                Sum(group, FinancialRecordTypes.Expense)))
            .ToArray();
        var lastEventAtUtc = records.Count == 0
            ? null
            : records.Max(record => record.ChangedAtUtc);
        var normalizedAsOfUtc = asOfUtc.ToUniversalTime();
        var isStale = lastEventAtUtc is null ||
            normalizedAsOfUtc - lastEventAtUtc.Value > staleAfter;

        return new FinancialSummaryReadModel(
            normalizedUserIdHash,
            normalizedCurrency,
            referenceDate,
            day,
            week,
            month,
            month.BalanceDelta,
            categories,
            lastEventAtUtc,
            isStale);
    }

    private static FinancialPeriodTotals BuildTotals(
        IEnumerable<FinancialRecordProjection> records,
        DateOnly from,
        DateOnly to)
    {
        var periodRecords = records
            .Where(record => record.Date >= from && record.Date <= to)
            .ToArray();
        return new FinancialPeriodTotals(
            from,
            to,
            Sum(periodRecords, FinancialRecordTypes.Income),
            Sum(periodRecords, FinancialRecordTypes.Expense));
    }

    private static decimal Sum(
        IEnumerable<FinancialRecordProjection> records,
        string recordType) =>
        records
            .Where(record => record.RecordType == recordType)
            .Sum(record => record.Amount);

    private static string ReadRecordType(string eventType)
    {
        if (eventType == FinancialRecordEventTypes.IncomeCreated ||
            eventType == FinancialRecordEventTypes.IncomeUpdated ||
            eventType == FinancialRecordEventTypes.IncomeArchived ||
            eventType == FinancialRecordEventTypes.IncomeRestored)
        {
            return FinancialRecordTypes.Income;
        }

        if (eventType == FinancialRecordEventTypes.ExpenseCreated ||
            eventType == FinancialRecordEventTypes.ExpenseUpdated ||
            eventType == FinancialRecordEventTypes.ExpenseArchived ||
            eventType == FinancialRecordEventTypes.ExpenseRestored)
        {
            return FinancialRecordTypes.Expense;
        }

        throw new ArgumentException("Unsupported financial record event type.", nameof(eventType));
    }

    private static void Validate(
        IntegrationEventEnvelope<FinancialRecordChangedV1> envelope,
        FinancialRecordChangedV1 payload)
    {
        if (envelope.SchemaVersion != FinancialRecordEventTypes.SchemaVersion ||
            string.IsNullOrWhiteSpace(envelope.UserIdHash) ||
            string.IsNullOrWhiteSpace(payload.RecordId) ||
            payload.Amount <= 0 ||
            string.IsNullOrWhiteSpace(payload.Currency) ||
            payload.Currency.Length != 3 ||
            string.IsNullOrWhiteSpace(payload.CategoryId) ||
            payload.Date == default ||
            payload.Revision < 0 ||
            payload.ChangedAtUtc == default ||
            (payload.Status != FinancialRecordProjectionStatuses.Active &&
             payload.Status != FinancialRecordProjectionStatuses.Archived))
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
