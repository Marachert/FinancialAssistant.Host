using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FinancialAssistant.Income.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.Income.Infrastructure.Persistence;

internal static partial class IncomeRecordSnapshot
{
    public static void ValidateIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value != value.Trim() || value.Any(char.IsControl))
        { throw new ArgumentException("Invalid financial record identifier."); }
    }

    public static void Validate(IncomeRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        ValidateIdentifier(record.TransactionId);
        ValidateIdentifier(record.UserId);
        if (record.SourceDraftId is not null) { ValidateIdentifier(record.SourceDraftId); }
        if (record.Amount <= 0 || record.Amount > 999_999_999_999.99m || decimal.Round(record.Amount, 2) != record.Amount
            || record.Currency is not ("USD" or "EUR" or "GBP" or "UAH") || record.CategoryId is null
            || !CategoryPattern().IsMatch(record.CategoryId) || record.CategoryId.Length > 100
            || record.Merchant is not null && (record.Merchant.Length > 120 || record.Merchant != record.Merchant.Trim())
            || record.Date == default || record.ConfirmedAtUtc == default || record.ConfirmedAtUtc.Offset != TimeSpan.Zero
            || record.Status is not (IncomeRecordStatuses.Active or IncomeRecordStatuses.Archived)
            || record.Revision < 0 || record.Revision == long.MaxValue
            || record.UpdatedAtUtc is not null && (record.UpdatedAtUtc == default || record.UpdatedAtUtc.Value.Offset != TimeSpan.Zero
                || record.UpdatedAtUtc < record.ConfirmedAtUtc)
            || record.Revision == 0 && record.UpdatedAtUtc is not null || record.Revision > 0 && record.UpdatedAtUtc is null
            || record.Origin is not (IncomeRecordOrigins.Manual or IncomeRecordOrigins.ConfirmedTransaction)
            || (record.Origin == IncomeRecordOrigins.Manual) != (record.SourceDraftId is null))
        { throw new ArgumentException("Invalid financial record snapshot."); }
    }

    public static byte[] Encode(IncomeRecord record)
    {
        Validate(record);
        return OwnedRecordCodec.Encode(1, new State(record.TransactionId, record.UserId, record.SourceDraftId,
            record.Amount, record.Currency, record.CategoryId, record.Merchant, record.Date, record.ConfirmedAtUtc,
            record.Status, record.Revision, record.UpdatedAtUtc, record.Origin));
    }

    public static IncomeRecord Decode(DurableRecord durable, string owner, string? id = null, bool immutable = false)
    {
        try
        {
            var state = OwnedRecordCodec.Decode<State>(1, durable);
            var record = new IncomeRecord(state.TransactionId, state.UserId, state.SourceDraftId, state.Amount,
                state.Currency, state.CategoryId, state.Merchant, state.Date, state.ConfirmedAtUtc, state.Status,
                state.Revision, state.UpdatedAtUtc, state.Origin);
            Validate(record);
            if (record.UserId != owner || id is not null && record.TransactionId != id
                || record.Revision != durable.Revision || immutable && record.Revision != 0)
            { throw new StorageUnavailableException(); }
            return record;
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    [GeneratedRegex(@"^income\.[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CategoryPattern();

    private sealed record State([property: JsonRequired] string TransactionId, [property: JsonRequired] string UserId,
        [property: JsonRequired] string? SourceDraftId, [property: JsonRequired] decimal Amount,
        [property: JsonRequired] string Currency, [property: JsonRequired] string CategoryId,
        [property: JsonRequired] string? Merchant, [property: JsonRequired] DateOnly Date,
        [property: JsonRequired] DateTimeOffset ConfirmedAtUtc, [property: JsonRequired] string Status,
        [property: JsonRequired] long Revision, [property: JsonRequired] DateTimeOffset? UpdatedAtUtc,
        [property: JsonRequired] string Origin);
}
