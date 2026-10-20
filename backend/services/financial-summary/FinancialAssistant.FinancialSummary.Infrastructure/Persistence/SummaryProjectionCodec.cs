using System.Security.Cryptography;
using System.Text.Json.Serialization;
using FinancialAssistant.FinancialSummary.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.FinancialSummary.Infrastructure.Persistence;

public static class SummaryProjectionCodec
{
    public static void Owner(string owner)
    {
        if (owner is not { Length: 64 } || owner.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
        { throw new ArgumentException("Invalid Summary owner hash."); }
    }

    public static void Identifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value != value.Trim() ||
            value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or '~')))
        { throw new ArgumentException("Invalid Summary identifier."); }
    }

    public static void Currency(string currency)
    {
        if (currency is not { Length: 3 } || currency.Any(c => c is < 'A' or > 'Z'))
        { throw new ArgumentException("Invalid Summary currency."); }
    }

    public static void Validate(FinancialRecordProjection value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Owner(value.UserIdHash); Identifier(value.RecordId); Identifier(value.EventId); Identifier(value.CategoryId);
        Currency(value.Currency);
        if (value.RecordType is not (FinancialRecordTypes.Income or FinancialRecordTypes.Expense) ||
            !value.CategoryId.StartsWith(value.RecordType + ".", StringComparison.Ordinal) ||
            value.Amount <= 0 || value.Date == default || value.Revision is < 0 or long.MaxValue ||
            value.Status is not (FinancialRecordProjectionStatuses.Active or FinancialRecordProjectionStatuses.Archived) ||
            value.Origin is not ("manual" or "confirmed_transaction") ||
            value.ChangedAtUtc == default || value.ChangedAtUtc.Offset != TimeSpan.Zero)
        { throw new ArgumentException("Invalid Summary projection."); }
    }

    public static byte[] Encode(FinancialRecordProjection value)
    {
        Validate(value);
        return OwnedRecordCodec.Encode(1, new State(value.RecordType, value.RecordId, value.UserIdHash, value.Amount,
            value.Currency, value.CategoryId, value.Date, value.Status, value.Revision, value.Origin,
            value.ChangedAtUtc, value.EventId));
    }

    public static FinancialRecordProjection Decode(DurableRecord durable, string owner, string? recordKey = null)
    {
        try
        {
            var state = OwnedRecordCodec.Decode<State>(1, durable);
            var value = new FinancialRecordProjection(state.RecordType, state.RecordId, state.UserIdHash, state.Amount,
                state.Currency, state.CategoryId, state.Date, state.Status, state.Revision, state.Origin,
                state.ChangedAtUtc, state.EventId);
            Validate(value);
            if (value.UserIdHash != owner || recordKey is not null && RecordKey(value) != recordKey)
            { throw new StorageUnavailableException(); }
            return value;
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    public static string RecordKey(FinancialRecordProjection value) => value.RecordType + ":" + value.RecordId;

    public static string Fingerprint(FinancialRecordProjection value) => Convert.ToHexString(SHA256.HashData(
        Encode(value with { Amount = OwnedRecordCodec.CanonicalDecimal(value.Amount) })));

    // A different event at an already applied revision is not an idempotent retry.
    public static bool ShouldReplace(FinancialRecordProjection? current, FinancialRecordProjection incoming)
    {
        Validate(incoming);
        if (current is null) { return true; }
        Validate(current);
        if (current.UserIdHash != incoming.UserIdHash || RecordKey(current) != RecordKey(incoming) ||
            current.Origin != incoming.Origin)
        { throw new StorageConflictException(); }
        if (current.Revision > incoming.Revision) { return false; }
        if (current.Revision == incoming.Revision)
        {
            if (current != incoming) { throw new StorageConflictException(); }
            return false;
        }
        if (incoming.ChangedAtUtc < current.ChangedAtUtc || incoming.EventId == current.EventId)
        { throw new StorageConflictException(); }
        return true;
    }

    private sealed record State(
        [property: JsonRequired] string RecordType, [property: JsonRequired] string RecordId,
        [property: JsonRequired] string UserIdHash, [property: JsonRequired] decimal Amount,
        [property: JsonRequired] string Currency, [property: JsonRequired] string CategoryId,
        [property: JsonRequired] DateOnly Date, [property: JsonRequired] string Status,
        [property: JsonRequired] long Revision, [property: JsonRequired] string Origin,
        [property: JsonRequired] DateTimeOffset ChangedAtUtc, [property: JsonRequired] string EventId);
}
