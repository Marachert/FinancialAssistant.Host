using System.Text.RegularExpressions;
using FinancialAssistant.Expense.Contracts;
using FinancialAssistant.Expense.Domain;

namespace FinancialAssistant.Expense.Application;

public sealed partial class ExpenseManagementService : IExpenseManagementService
{
    private const decimal MaximumAmount = 999_999_999_999.99m;
    private const int MaximumMutationAttempts = 5;

    private static readonly HashSet<string> SupportedCurrencies =
        new(StringComparer.Ordinal) { "EUR", "GBP", "UAH", "USD" };

    private readonly IExpenseRecordStore store;
    private readonly TimeProvider timeProvider;

    public ExpenseManagementService(IExpenseRecordStore store, TimeProvider timeProvider)
    {
        this.store = store;
        this.timeProvider = timeProvider;
    }

    public async Task<ExpenseRecordResponse> CreateAsync(
        string userId,
        CreateExpenseRequest request,
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
            var expenseId = Guid.NewGuid().ToString("N");
            var record = new ExpenseRecord(
                expenseId,
                normalizedUserId,
                SourceDraftId: null,
                values.Amount,
                values.Currency,
                values.CategoryId,
                values.Merchant,
                values.Date,
                now,
                ExpenseRecordStatuses.Active,
                Revision: 0,
                UpdatedAtUtc: null,
                ExpenseRecordOrigins.Manual);
            if (await store.CreateAsync(record, cancellationToken))
            {
                return ToResponse(record);
            }
        }
    }

    public async Task<ExpenseRecordResponse?> GetAsync(
        string userId,
        string expenseId,
        CancellationToken cancellationToken)
    {
        var record = await store.GetAsync(
            NormalizeRequired(userId, nameof(userId)),
            NormalizeRequired(expenseId, nameof(expenseId)),
            cancellationToken);
        return record is null ? null : ToResponse(record);
    }

    public async Task<ExpenseListResponse> ListAsync(
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
            .Where(record => record.Status == ExpenseRecordStatuses.Active)
            .GroupBy(record => record.Currency, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ExpenseTotalResponse(group.Key, group.Sum(record => record.Amount)))
            .ToArray();
        return new ExpenseListResponse(records.Select(ToResponse).ToArray(), totals);
    }

    public async Task<ExpenseRecordResponse?> UpdateAsync(
        string userId,
        string expenseId,
        UpdateExpenseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedUserId = NormalizeRequired(userId, nameof(userId));
        var normalizedExpenseId = NormalizeRequired(expenseId, nameof(expenseId));
        var values = ValidateValues(
            request.Amount,
            request.Currency,
            request.CategoryId,
            request.Merchant,
            request.Date);

        return await MutateAsync(
            normalizedUserId,
            normalizedExpenseId,
            current =>
            {
                if (current.Status != ExpenseRecordStatuses.Active)
                {
                    throw new ExpenseRecordNotEditableException(current.Status);
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
            cancellationToken);
    }

    public Task<ExpenseRecordResponse?> ArchiveAsync(
        string userId,
        string expenseId,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(userId, expenseId, ExpenseRecordStatuses.Archived, cancellationToken);

    public Task<ExpenseRecordResponse?> RestoreAsync(
        string userId,
        string expenseId,
        CancellationToken cancellationToken) =>
        ChangeStatusAsync(userId, expenseId, ExpenseRecordStatuses.Active, cancellationToken);

    private async Task<ExpenseRecordResponse?> ChangeStatusAsync(
        string userId,
        string expenseId,
        string targetStatus,
        CancellationToken cancellationToken)
    {
        var normalizedUserId = NormalizeRequired(userId, nameof(userId));
        var normalizedExpenseId = NormalizeRequired(expenseId, nameof(expenseId));
        return await MutateAsync(
            normalizedUserId,
            normalizedExpenseId,
            current =>
            {
                if (current.Status == targetStatus)
                {
                    return current;
                }

                if (targetStatus == ExpenseRecordStatuses.Active)
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
            cancellationToken);
    }

    private async Task<ExpenseRecordResponse?> MutateAsync(
        string userId,
        string expenseId,
        Func<ExpenseRecord, ExpenseRecord> mutate,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaximumMutationAttempts; attempt++)
        {
            var current = await store.GetAsync(userId, expenseId, cancellationToken);
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
                expenseId,
                current.Revision,
                replacement,
                cancellationToken);
            if (result.Replaced)
            {
                return ToResponse(result.Record!);
            }
        }

        throw new ExpenseMutationConflictException();
    }

    private ValidatedExpenseValues ValidateValues(
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
            !ExpenseCategoryPattern().IsMatch(normalizedCategoryId))
        {
            throw new ArgumentException(
                "Category must be a valid expense.* identifier.",
                nameof(categoryId));
        }

        var normalizedMerchant = NormalizeMerchant(merchant);
        var currentDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (date == default ||
            date < currentDate.AddYears(-10) ||
            date > currentDate.AddDays(366))
        {
            throw new ArgumentException(
                "Date must be within the supported Expense period.",
                nameof(date));
        }

        return new ValidatedExpenseValues(
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
                "Expense period must be valid, ordered, and no longer than 10 years.",
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

    private static ExpenseRecordResponse ToResponse(ExpenseRecord record) =>
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

    [GeneratedRegex(@"^expense\.[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ExpenseCategoryPattern();

    private sealed record ValidatedExpenseValues(
        decimal Amount,
        string Currency,
        string CategoryId,
        string? Merchant,
        DateOnly Date);
}
