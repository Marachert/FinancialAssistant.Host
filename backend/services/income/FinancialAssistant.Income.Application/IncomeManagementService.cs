using System.Diagnostics;
using System.Text.RegularExpressions;
using FinancialAssistant.Income.Contracts;
using FinancialAssistant.Income.Domain;
using FinancialAssistant.Shared.Contracts.Events;

namespace FinancialAssistant.Income.Application;

public sealed partial class IncomeManagementService : IIncomeManagementService
{
    private const decimal MaximumAmount = 999_999_999_999.99m;
    private const int MaximumMutationAttempts = 5;

    private static readonly HashSet<string> SupportedCurrencies =
        new(StringComparer.Ordinal) { "EUR", "GBP", "UAH", "USD" };

    private readonly IIncomeRecordStore store;
    private readonly TimeProvider timeProvider;
    private readonly IIncomeRecordEventPublisher eventPublisher;

    public IncomeManagementService(
        IIncomeRecordStore store,
        TimeProvider timeProvider,
        IIncomeRecordEventPublisher? eventPublisher = null)
    {
        this.store = store;
        this.timeProvider = timeProvider;
        this.eventPublisher = eventPublisher ?? NullIncomeRecordEventPublisher.Instance;
    }

    public async Task<IncomeRecordResponse> CreateAsync(
        string userId,
        CreateIncomeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedUserId = NormalizeRequired(userId, nameof(userId));
        var values = ValidateValues(
            request.Amount,
            request.Currency,
            request.CategoryId,
            request.Merchant,
            request.Date);
        var now = timeProvider.GetUtcNow().ToUniversalTime();

        while (true)
        {
            var incomeId = Guid.NewGuid().ToString("N");
            var record = new IncomeRecord(
                incomeId,
                normalizedUserId,
                SourceDraftId: null,
                values.Amount,
                values.Currency,
                values.CategoryId,
                values.Merchant,
                values.Date,
                now,
                IncomeRecordStatuses.Active,
                Revision: 0,
                UpdatedAtUtc: null,
                IncomeRecordOrigins.Manual);
            if (await store.CreateAsync(record, cancellationToken))
            {
                var correlationId = CreateCorrelationId();
                await eventPublisher.PublishAsync(
                    FinancialRecordEventTypes.IncomeCreated,
                    record,
                    correlationId,
                    correlationId,
                    cancellationToken);
                return ToResponse(record);
            }
        }
    }

    public async Task<IncomeRecordResponse?> GetAsync(
        string userId,
        string incomeId,
        CancellationToken cancellationToken)
    {
        var record = await store.GetAsync(
            NormalizeRequired(userId, nameof(userId)),
            NormalizeRequired(incomeId, nameof(incomeId)),
            cancellationToken);
        return record is null ? null : ToResponse(record);
    }

    public async Task<IncomeListResponse> ListAsync(
        string userId,
        DateOnly from,
        DateOnly to,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeRequired(userId, nameof(userId));
        ValidatePeriod(from, to);
        var records = await store.ListAsync(
            normalizedUserId,
            from,
            to,
            includeArchived,
            cancellationToken);
        var totals = records
            .Where(record => record.Status == IncomeRecordStatuses.Active)
            .GroupBy(record => record.Currency, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new IncomeTotalResponse(group.Key, group.Sum(record => record.Amount)))
            .ToArray();
        return new IncomeListResponse(records.Select(ToResponse).ToArray(), totals);
    }

    public async Task<IncomeRecordResponse?> UpdateAsync(
        string userId,
        string incomeId,
        UpdateIncomeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedUserId = NormalizeRequired(userId, nameof(userId));
        var normalizedIncomeId = NormalizeRequired(incomeId, nameof(incomeId));
        var values = ValidateValues(
            request.Amount,
            request.Currency,
            request.CategoryId,
            request.Merchant,
            request.Date);

        return await MutateAsync(
            normalizedUserId,
            normalizedIncomeId,
            current =>
            {
                if (current.Status != IncomeRecordStatuses.Active)
                {
                    throw new IncomeRecordNotEditableException(current.Status);
                }

                return current with
                {
                    Amount = values.Amount,
                    Currency = values.Currency,
                    CategoryId = values.CategoryId,
                    Merchant = values.Merchant,
                    Date = values.Date,
                    Revision = current.Revision + 1,
                    UpdatedAtUtc = timeProvider.GetUtcNow().ToUniversalTime()
                };
            },
            FinancialRecordEventTypes.IncomeUpdated,
            cancellationToken);
    }

    public Task<IncomeRecordResponse?> ArchiveAsync(
        string userId,
        string incomeId,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(userId, incomeId, IncomeRecordStatuses.Archived, cancellationToken);

    public Task<IncomeRecordResponse?> RestoreAsync(
        string userId,
        string incomeId,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(userId, incomeId, IncomeRecordStatuses.Active, cancellationToken);

    private async Task<IncomeRecordResponse?> ChangeStatusAsync(
        string userId,
        string incomeId,
        string targetStatus,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeRequired(userId, nameof(userId));
        var normalizedIncomeId = NormalizeRequired(incomeId, nameof(incomeId));
        return await MutateAsync(
            normalizedUserId,
            normalizedIncomeId,
            current =>
            {
                if (current.Status == targetStatus)
                {
                    return current;
                }

                if (targetStatus == IncomeRecordStatuses.Active)
                {
                    _ = ValidateValues(
                        current.Amount,
                        current.Currency,
                        current.CategoryId,
                        current.Merchant,
                        current.Date);
                }

                return current with
                {
                    Status = targetStatus,
                    Revision = current.Revision + 1,
                    UpdatedAtUtc = timeProvider.GetUtcNow().ToUniversalTime()
                };
            },
            targetStatus == IncomeRecordStatuses.Archived
                ? FinancialRecordEventTypes.IncomeArchived
                : FinancialRecordEventTypes.IncomeRestored,
            cancellationToken);
    }

    private async Task<IncomeRecordResponse?> MutateAsync(
        string userId,
        string incomeId,
        Func<IncomeRecord, IncomeRecord> mutate,
        string eventType,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaximumMutationAttempts; attempt++)
        {
            var current = await store.GetAsync(userId, incomeId, cancellationToken);
            if (current is null)
            {
                return null;
            }

            var replacement = mutate(current);
            if (ReferenceEquals(replacement, current))
            {
                return ToResponse(current);
            }

            var result = await store.ReplaceAsync(
                userId,
                incomeId,
                current.Revision,
                replacement,
                cancellationToken);
            if (result.Replaced)
            {
                var correlationId = CreateCorrelationId();
                await eventPublisher.PublishAsync(
                    eventType,
                    result.Record!,
                    correlationId,
                    correlationId,
                    cancellationToken);
                return ToResponse(result.Record!);
            }
        }

        throw new IncomeMutationConflictException();
    }

    private static string CreateCorrelationId()
    {
        var traceId = Activity.Current?.TraceId.ToString();
        return string.IsNullOrWhiteSpace(traceId)
            ? Guid.NewGuid().ToString("N")
            : traceId;
    }

    private ValidatedIncomeValues ValidateValues(
        decimal amount,
        string? currency,
        string? categoryId,
        string? merchant,
        DateOnly date)
    {
        if (amount <= 0 || amount > MaximumAmount)
        {
            throw new ArgumentException(
                $"Amount must be greater than zero and no more than {MaximumAmount}.",
                nameof(amount));
        }

        var normalizedAmount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        if (normalizedAmount <= 0)
        {
            throw new ArgumentException("Amount rounds to zero.", nameof(amount));
        }

        var normalizedCurrency = currency?.Trim().ToUpperInvariant();
        if (normalizedCurrency is null || !SupportedCurrencies.Contains(normalizedCurrency))
        {
            throw new ArgumentException("Currency is not supported.", nameof(currency));
        }

        var normalizedCategoryId = categoryId?.Trim().ToLowerInvariant();
        if (normalizedCategoryId is null ||
            !IncomeCategoryPattern().IsMatch(normalizedCategoryId))
        {
            throw new ArgumentException(
                "Category must be a valid income.* identifier.",
                nameof(categoryId));
        }

        var normalizedMerchant = NormalizeMerchant(merchant);
        var currentDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (date == default ||
            date < currentDate.AddYears(-10) ||
            date > currentDate.AddDays(366))
        {
            throw new ArgumentException(
                "Date must be within the supported Income period.",
                nameof(date));
        }

        return new ValidatedIncomeValues(
            normalizedAmount,
            normalizedCurrency,
            normalizedCategoryId,
            normalizedMerchant,
            date);
    }

    private static string? NormalizeMerchant(string? merchant)
    {
        if (string.IsNullOrWhiteSpace(merchant))
        {
            return null;
        }

        var normalized = string.Join(
            ' ',
            merchant.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length > 120)
        {
            throw new ArgumentException(
                "Merchant cannot exceed 120 characters.",
                nameof(merchant));
        }

        return normalized;
    }

    private static void ValidatePeriod(DateOnly from, DateOnly to)
    {
        if (from == default || to == default || from > to || to.DayNumber - from.DayNumber > 3660)
        {
            throw new ArgumentException(
                "Income period must be valid, ordered, and no longer than 10 years.",
                nameof(from));
        }
    }

    private static string NormalizeRequired(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > 200)
        {
            throw new ArgumentException("Value cannot exceed 200 characters.", parameterName);
        }

        return normalized;
    }

    private static IncomeRecordResponse ToResponse(IncomeRecord record) =>
        new(
            record.TransactionId,
            record.Status,
            record.Origin,
            record.Amount,
            record.Currency,
            record.CategoryId,
            record.Merchant,
            record.Date,
            record.ConfirmedAtUtc,
            record.UpdatedAtUtc,
            record.Revision);

    [GeneratedRegex(@"^income\.[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IncomeCategoryPattern();

    private sealed record ValidatedIncomeValues(
        decimal Amount,
        string Currency,
        string CategoryId,
        string? Merchant,
        DateOnly Date);
}
