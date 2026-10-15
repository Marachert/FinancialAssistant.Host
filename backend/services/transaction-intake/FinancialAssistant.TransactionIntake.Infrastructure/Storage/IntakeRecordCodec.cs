using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FinancialAssistant.Shared.Persistence;
using FinancialAssistant.TransactionIntake.Contracts;
using FinancialAssistant.TransactionIntake.Domain.Drafts;

namespace FinancialAssistant.TransactionIntake.Infrastructure.Storage;

internal static partial class IntakeRecordCodec
{
    public static void Identifier(string value, int maximum = 200)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum || value != value.Trim() || value.Any(char.IsControl))
        { throw new ArgumentException("Invalid Intake identifier."); }
    }

    public static void InputFingerprint(string value)
    {
        if (value is null || value.Length != 64 || !value.All(char.IsAsciiHexDigit))
        { throw new ArgumentException("Invalid Intake fingerprint."); }
    }

    public static string SourceFingerprint(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 2000 || input.Any(char.IsControl))
        { throw new ArgumentException("Invalid Intake source payload."); }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    public static void Validate(TransactionDraft value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Identifier(value.Id);
        Identifier(value.UserId);
        InputFingerprint(value.InputFingerprint);
        if (!TransactionTypes.IsSupported(value.Type) || value.Confidence is < 0 or > 1
            || value.Amount is not null && (value.Amount <= 0 || value.Amount > 999_999_999_999.99m || decimal.Round(value.Amount.Value, 2) != value.Amount)
            || value.Currency is not null && value.Currency is not ("EUR" or "GBP" or "UAH" or "USD")
            || value.CategoryId is not null && (value.CategoryId.Length > 100 || !CategoryPattern().IsMatch(value.CategoryId)
                || !value.CategoryId.StartsWith(value.Type + ".", StringComparison.Ordinal))
            || value.Merchant is not null && !Text(value.Merchant, 120)
            || value.Note is not null && !Text(value.Note, 500) || value.Date == default(DateOnly)
            || value.CreatedAtUtc == default || value.CreatedAtUtc.Offset != TimeSpan.Zero || value.Revision < 0 || value.Revision == long.MaxValue
            || value.Status is not (TransactionDraftStatuses.Draft or TransactionDraftStatuses.Confirming or TransactionDraftStatuses.Confirmed or TransactionDraftStatuses.Rejected)
            || value.InputSource is not (TransactionInputSources.Text or TransactionInputSources.VoiceTranscript or TransactionInputSources.ReceiptOcr or TransactionInputSources.ManualForm)
            || value.Suggestion is null || value.Suggestion.Source is not (TransactionDraftSuggestionSources.AiNaturalLanguage or TransactionDraftSuggestionSources.ReceiptOcr)
            || value.Suggestion.Confidence != value.Confidence || !Text(value.Suggestion.ReviewMessage, 280)
            || value.Suggestion.SourceReferenceId is not null && !Text(value.Suggestion.SourceReferenceId, 100))
        { throw new ArgumentException("Invalid Intake draft snapshot."); }
        Codes(value.Ambiguities);
        Codes(value.Suggestion.Ambiguities);
        Codes(value.Suggestion.MissingFields);
        if (!value.Ambiguities.SequenceEqual(value.Suggestion.Ambiguities)
            || value.RequiresReview != (value.Ambiguities.Count > 0 || value.Suggestion.MissingFields.Count > 0)
            || (value.InputSource == TransactionInputSources.ReceiptOcr) != (value.Suggestion.Source == TransactionDraftSuggestionSources.ReceiptOcr)
            || value.InputSource == TransactionInputSources.ReceiptOcr && value.Suggestion.SourceReferenceId is null
            || !value.RequiresReview && (value.Confidence < 0.75m || value.Type == TransactionTypes.Unknown || value.Amount is null
                || value.Currency is null || value.Date is null || value.Type != TransactionTypes.Transfer && value.CategoryId is null)
            || value.Status is TransactionDraftStatuses.Confirming or TransactionDraftStatuses.Confirmed &&
                (value.Type is not (TransactionTypes.Income or TransactionTypes.Expense) || value.Amount is null
                    || value.Currency is null || value.CategoryId is null || value.Date is null || value.RequiresReview))
        { throw new ArgumentException("Invalid Intake draft lifecycle or suggestion metadata."); }
    }

    public static byte[] Encode(TransactionDraft value)
    {
        Validate(value);
        var suggestion = value.Suggestion;
        return OwnedRecordCodec.Encode(1, new DraftState(value.Id, value.UserId, value.InputFingerprint, value.Type, value.Amount,
            value.Currency, value.CategoryId, value.Merchant, value.Date, value.Confidence, value.Ambiguities.ToArray(), value.RequiresReview,
            new SuggestionState(suggestion.Source, suggestion.SourceReferenceId, suggestion.Confidence, suggestion.Ambiguities.ToArray(),
                suggestion.MissingFields.ToArray(), suggestion.ReviewMessage), value.CreatedAtUtc, value.InputSource, value.Note, value.Status, value.Revision));
    }

    public static TransactionDraft Decode(DurableRecord durable, string owner, string? id = null)
    {
        try
        {
            var state = OwnedRecordCodec.Decode<DraftState>(1, durable);
            if (state.Suggestion is null || state.Ambiguities is null || state.Suggestion.Ambiguities is null || state.Suggestion.MissingFields is null)
            { throw new StorageUnavailableException(); }
            var suggestion = state.Suggestion;
            var value = new TransactionDraft(state.Id, state.UserId, state.InputFingerprint, state.Type, state.Amount, state.Currency,
                state.CategoryId, state.Merchant, state.Date, state.Confidence, Array.AsReadOnly(state.Ambiguities), state.RequiresReview,
                new TransactionDraftSuggestionMetadata(suggestion.Source, suggestion.SourceReferenceId, suggestion.Confidence,
                    Array.AsReadOnly(suggestion.Ambiguities), Array.AsReadOnly(suggestion.MissingFields), suggestion.ReviewMessage),
                state.CreatedAtUtc, state.InputSource, state.Note, state.Status, state.Revision);
            Validate(value);
            if (value.UserId != owner || id is not null && value.Id != id || value.Revision != durable.Revision)
            { throw new StorageUnavailableException(); }
            return value;
        }
        catch (ArgumentException) { throw new StorageUnavailableException(); }
    }

    public static void Creation(TransactionDraftCreatedIntegrationEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Identifier(value.UserId); Identifier(value.DraftId); Identifier(value.EventId);
        Identifier(value.JobId); Identifier(value.SourcePayloadReferenceId);
        if (value.OccurredAtUtc == default || value.OccurredAtUtc.Offset != TimeSpan.Zero)
        { throw new ArgumentException("Invalid Intake creation event."); }
    }

    public static void Confirmation(TransactionConfirmedIntegrationEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Identifier(value.UserId); Identifier(value.DraftId); Identifier(value.EventId); Identifier(value.TransactionId);
        Identifier(value.CorrelationId, 128);
        if (value.TransactionType is not (TransactionTypes.Income or TransactionTypes.Expense)
            || value.Amount <= 0 || value.Amount > 999_999_999_999.99m || decimal.Round(value.Amount, 2) != value.Amount
            || value.Currency is not ("EUR" or "GBP" or "UAH" or "USD") || value.CategoryId is null
            || !CategoryPattern().IsMatch(value.CategoryId) || !value.CategoryId.StartsWith(value.TransactionType + ".", StringComparison.Ordinal)
            || value.Date == default || value.ConfirmedAtUtc == default || value.ConfirmedAtUtc.Offset != TimeSpan.Zero)
        { throw new ArgumentException("Invalid Intake confirmation event."); }
    }

    private static bool Text(string value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum
        && value == value.Trim() && !value.Any(char.IsControl);
    private static void Codes(IReadOnlyList<string> codes)
    {
        if (codes is null || codes.Count > 50 || codes.Any(code => code is null || code.Length is < 1 or > 64
                || code.Any(character => !(char.IsLower(character) || char.IsDigit(character) || character == '_')))
            || !codes.SequenceEqual(codes.Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal)))
        { throw new ArgumentException("Invalid Intake suggestion codes."); }
    }
    [GeneratedRegex(@"^(income|expense|transfer)\.[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CategoryPattern();

    private sealed record DraftState([property: JsonRequired] string Id, [property: JsonRequired] string UserId,
        [property: JsonRequired] string InputFingerprint, [property: JsonRequired] string Type, [property: JsonRequired] decimal? Amount,
        [property: JsonRequired] string? Currency, [property: JsonRequired] string? CategoryId, [property: JsonRequired] string? Merchant,
        [property: JsonRequired] DateOnly? Date, [property: JsonRequired] decimal Confidence, [property: JsonRequired] string[] Ambiguities,
        [property: JsonRequired] bool RequiresReview, [property: JsonRequired] SuggestionState Suggestion,
        [property: JsonRequired] DateTimeOffset CreatedAtUtc, [property: JsonRequired] string InputSource,
        [property: JsonRequired] string? Note, [property: JsonRequired] string Status, [property: JsonRequired] long Revision);
    private sealed record SuggestionState([property: JsonRequired] string Source, [property: JsonRequired] string? SourceReferenceId,
        [property: JsonRequired] decimal Confidence, [property: JsonRequired] string[] Ambiguities,
        [property: JsonRequired] string[] MissingFields, [property: JsonRequired] string ReviewMessage);
}
