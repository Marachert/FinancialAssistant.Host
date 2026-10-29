using System.Security.Cryptography;
using System.Text.Json.Serialization;
using FinancialAssistant.FinancialScore.Domain;
using FinancialAssistant.Shared.Persistence;

namespace FinancialAssistant.FinancialScore.Infrastructure.Persistence;

internal sealed record ScoreState(
    [property: JsonRequired] string Owner,
    [property: JsonRequired] List<ScoreProjectionState> Projections,
    [property: JsonRequired] List<ScoreCalculationState> Calculations,
    [property: JsonRequired] List<ScoreSourceState> Sources,
    [property: JsonRequired] List<ScorePublicationState> Publications)
{
    public static ScoreState Empty(string owner) => new(owner, [], [], [], []);
}

internal sealed record ScoreSourceState([property: JsonRequired] string EventId,
    [property: JsonRequired] string Fingerprint);

internal sealed record ScoreProjectionState([property: JsonRequired] string Type,
    [property: JsonRequired] string Id, [property: JsonRequired] decimal Amount,
    [property: JsonRequired] string Currency, [property: JsonRequired] DateOnly Date,
    [property: JsonRequired] string Status, [property: JsonRequired] long Revision,
    [property: JsonRequired] DateTimeOffset ChangedAtUtc, [property: JsonRequired] string EventId)
{
    public FinancialScoreRecordProjection ToDomain(string owner) =>
        new(Type, Id, owner, Amount, Currency, Date, Status, Revision, ChangedAtUtc, EventId);
    public static ScoreProjectionState From(FinancialScoreRecordProjection p) =>
        new(p.RecordType, p.RecordId, OwnedRecordCodec.CanonicalDecimal(p.Amount), p.Currency,
            p.Date, p.Status, p.Revision, p.ChangedAtUtc, p.EventId);
}

internal sealed record ScoreInputState([property: JsonRequired] string Code,
    [property: JsonRequired] decimal Value, [property: JsonRequired] string Unit);
internal sealed record ScoreFactorState([property: JsonRequired] string Code,
    [property: JsonRequired] decimal Contribution, [property: JsonRequired] string Explanation,
    [property: JsonRequired] ScoreInputState[] Inputs);
internal sealed record ScoreCalculationState([property: JsonRequired] string Id,
    [property: JsonRequired] string Source, [property: JsonRequired] string Currency,
    [property: JsonRequired] int Score, [property: JsonRequired] string Formula,
    [property: JsonRequired] ScoreFactorState[] Factors, [property: JsonRequired] DateTimeOffset AtUtc)
{
    public FinancialScoreCalculation ToDomain(string owner) => new(Id, Source, owner, Currency, Score,
        Formula, Factors.Select(f => new FinancialScoreFactor(f.Code, f.Contribution, f.Explanation,
            f.Inputs.Select(i => new FinancialScoreFactorInput(i.Code, i.Value, i.Unit)).ToArray())).ToArray(), AtUtc);
    public static ScoreCalculationState From(FinancialScoreCalculation c) => new(c.CalculationId,
        c.SourceEventId, c.Currency, c.Score, c.FormulaVersion, c.Factors.Select(f => new ScoreFactorState(
            f.Code, f.Contribution, f.Explanation, f.Inputs.Select(i => new ScoreInputState(i.Code, i.Value, i.Unit)).ToArray())).ToArray(), c.CalculatedAtUtc);
}

// Keep the immutable event and its compact acknowledgement together; cleanup never makes replay publish again.
internal sealed record ScorePublicationState([property: JsonRequired] string CalculationId,
    [property: JsonRequired] string OccurrenceId, [property: JsonRequired] string CorrelationId,
    [property: JsonRequired] DateTimeOffset? PublishedAtUtc);

public static class ScorePersistenceValidation
{
    public static void Owner(string owner)
    {
        if (owner is not { Length: 64 } || owner.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f')))
        { throw new ArgumentException("Invalid Score owner hash."); }
    }

    public static void Identifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200 || value != value.Trim() ||
            value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' or '~')))
        { throw new ArgumentException("Invalid Score identifier."); }
    }

    public static void Currency(string value)
    {
        if (value is not { Length: 3 } || value.Any(c => c is < 'A' or > 'Z'))
        { throw new ArgumentException("Invalid Score currency."); }
    }

    public static void Utc(DateTimeOffset value)
    {
        if (value == default || value.Offset != TimeSpan.Zero) { throw new ArgumentException("A UTC Score timestamp is required."); }
    }

    public static void Projection(FinancialScoreRecordProjection value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Owner(value.UserIdHash); Identifier(value.RecordId); Identifier(value.EventId); Currency(value.Currency); Utc(value.ChangedAtUtc);
        if (value.RecordType is not ("income" or "expense") || value.Status is not ("active" or "archived") ||
            value.Amount <= 0 || value.Date == default || value.Revision is < 0 or long.MaxValue)
        { throw new ArgumentException("Invalid Score projection."); }
    }

    public static void Calculation(FinancialScoreCalculation value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Owner(value.UserIdHash); Identifier(value.CalculationId); Identifier(value.SourceEventId);
        Currency(value.Currency); Utc(value.CalculatedAtUtc);
        if (value.Score is < 0 or > 100 || value.FormulaVersion != FinancialScoreFormula.Version ||
            value.Factors is null || value.Factors.Count != 5 || value.Factors.Select(f => f.Code).Distinct().Count() != 5)
        { throw new ArgumentException("Invalid deterministic Score calculation."); }
        var known = new[] { "budget_usage", "spending_trend", "income_consistency", "data_completeness", "penalty_cap" };
        foreach (var f in value.Factors)
        {
            if (!known.Contains(f.Code, StringComparer.Ordinal) || f.Inputs is null || f.Inputs.Count > 8 ||
                f.Contribution is < -100 or > 100 || string.IsNullOrWhiteSpace(f.Explanation) ||
                f.Explanation.Length > 500 || f.Explanation.Any(char.IsControl))
            { throw new ArgumentException("Invalid Score factor."); }
            foreach (var i in f.Inputs) { Identifier(i.Code); Identifier(i.Unit); }
        }
    }

    internal static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(OwnedRecordCodec.Encode(1, value)));

    internal static byte[] Encode(ScoreState value)
    {
        Validate(value);
        var bytes = OwnedRecordCodec.Encode(1, value);
        if (bytes.Length > 900_000) { throw new StorageUnavailableException(); }
        return bytes;
    }

    internal static ScoreState Decode(DurableRecord record, string owner)
    {
        try
        {
            var state = OwnedRecordCodec.Decode<ScoreState>(1, record);
            Validate(state);
            if (state.Owner != owner || record.Payload.Length > 900_000) { throw new StorageUnavailableException(); }
            return state;
        }
        catch (Exception exception) when (exception is ArgumentException or NullReferenceException or InvalidOperationException)
        { throw new StorageUnavailableException(); }
    }

    private static void Validate(ScoreState state)
    {
        Owner(state.Owner);
        if (state.Projections is null || state.Calculations is null || state.Sources is null || state.Publications is null ||
            state.Projections.Count > 1000 || state.Calculations.Count > 1000 || state.Sources.Count > 1000 ||
            state.Publications.Count > 1000 || state.Projections.DistinctBy(p => (p.Type, p.Id)).Count() != state.Projections.Count ||
            state.Calculations.DistinctBy(c => c.Id).Count() != state.Calculations.Count ||
            state.Sources.DistinctBy(s => s.EventId).Count() != state.Sources.Count ||
            state.Publications.DistinctBy(p => p.CalculationId).Count() != state.Publications.Count)
        { throw new StorageUnavailableException(); }
        foreach (var p in state.Projections) { Projection(p.ToDomain(state.Owner)); }
        foreach (var c in state.Calculations) { Calculation(c.ToDomain(state.Owner)); }
        if (state.Calculations.DistinctBy(c => (c.Source, c.Currency)).Count() != state.Calculations.Count)
        { throw new StorageUnavailableException(); }
        foreach (var s in state.Sources)
        {
            Identifier(s.EventId);
            if (s.Fingerprint is not { Length: 64 } || !s.Fingerprint.All(char.IsAsciiHexDigit)) { throw new StorageUnavailableException(); }
        }
        foreach (var p in state.Publications)
        {
            Identifier(p.OccurrenceId); Identifier(p.CorrelationId);
            var c = state.Calculations.SingleOrDefault(c => c.Id == p.CalculationId) ?? throw new StorageUnavailableException();
            if (p.PublishedAtUtc is { } at) { Utc(at); if (at < c.AtUtc) { throw new StorageUnavailableException(); } }
        }
        foreach (var c in state.Calculations)
        {
            if (!c.Source.StartsWith("score-default-source-", StringComparison.Ordinal) &&
                (!state.Sources.Any(s => s.EventId == c.Source) || !state.Publications.Any(p => p.CalculationId == c.Id)))
            { throw new StorageUnavailableException(); }
        }
    }
}
