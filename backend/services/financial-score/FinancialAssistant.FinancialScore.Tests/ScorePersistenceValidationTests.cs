using System.Text.Json.Nodes;
using FinancialAssistant.FinancialScore.Domain;
using FinancialAssistant.FinancialScore.Infrastructure.Persistence;
using FinancialAssistant.Shared.Contracts.Events;
using FinancialAssistant.Shared.Persistence;
using Xunit;

namespace FinancialAssistant.FinancialScore.Tests;

public sealed class ScorePersistenceValidationTests
{
    private static readonly string Owner = new('a', 64);
    private static readonly DateTimeOffset At = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IndependentScoreSourcePreservesExactDecimalAndRejectsUnsafeInput()
    {
        var source = Source(0.0000000000000000000000000001m);
        var projection = ScorePersistenceContext.ReadSource(source);
        Assert.Equal(source.Payload.Amount, projection.Amount);
        Assert.Equal("expense", projection.RecordType);
        Assert.Throws<ArgumentException>(() => ScorePersistenceValidation.Projection(projection with { UserIdHash = "not-an-owner" }));
        Assert.Throws<ArgumentException>(() => ScorePersistenceValidation.Projection(projection with { Currency = "usd" }));
        Assert.Throws<ArgumentException>(() => ScorePersistenceValidation.Projection(projection with { Revision = long.MaxValue }));
        Assert.Throws<ArgumentException>(() => ScorePersistenceValidation.Projection(projection with { RecordId = "private/unsafe" }));
        Assert.Throws<ArgumentException>(() => ScorePersistenceValidation.Projection(projection with { ChangedAtUtc = At.ToOffset(TimeSpan.FromHours(1)) }));
    }

    [Fact]
    public void DeterministicFactorStateIsValidatedWithoutChangingNeutralFormula()
    {
        var calculation = new FinancialScoreCalculator().Calculate("synthetic-score", "synthetic-source", Owner,
            "USD", [], FinancialScoreProfileSettings.Unconfigured, At);
        ScorePersistenceValidation.Calculation(calculation);
        Assert.Equal(50, calculation.Score);
        Assert.Throws<ArgumentException>(() => ScorePersistenceValidation.Calculation(calculation with { Score = 101 }));
        Assert.Throws<ArgumentException>(() => ScorePersistenceValidation.Calculation(calculation with { FormulaVersion = "llm-score" }));
        Assert.Throws<ArgumentException>(() => ScorePersistenceValidation.Calculation(calculation with { Factors = [] }));
        Assert.Throws<ArgumentException>(() => ScorePersistenceValidation.Calculation(calculation with
        { Factors = calculation.Factors.Select(f => f with { Code = "untrusted" }).ToArray() }));
    }

    [Fact]
    public void ProfileInputRequiresExplicitAuthorizedSnapshotAndNonnegativeBudget()
    {
        var snapshot = new ScoreProfileSnapshot(Owner, "USD", "synthetic-v1", 1, FinancialScoreProfileSettings.Unconfigured, At);
        PostgresFinancialScoreProfileSettingsProvider.Validate(snapshot);
        Assert.Throws<ArgumentException>(() => PostgresFinancialScoreProfileSettingsProvider.Validate(snapshot with { SourceRevision = -1 }));
        Assert.Throws<ArgumentException>(() => PostgresFinancialScoreProfileSettingsProvider.Validate(snapshot with { Settings = new(-1, false, false) }));
        Assert.Throws<ArgumentException>(() => PostgresFinancialScoreProfileSettingsProvider.Validate(snapshot with { SourceVersion = "" }));
    }

    [Fact]
    public void OwnedDtoRoundTripIsExactAndMissingRequiredFieldsFailClosed()
    {
        var state = ScoreState.Empty(Owner);
        state.Projections.Add(ScoreProjectionState.From(ScorePersistenceContext.ReadSource(Source(decimal.MaxValue))));
        var bytes = ScorePersistenceValidation.Encode(state);
        var decoded = ScorePersistenceValidation.Decode(new DurableRecord(3, bytes), Owner);
        Assert.Equal(decimal.MaxValue, decoded.Projections.Single().Amount);
        Assert.Throws<StorageUnavailableException>(() => ScorePersistenceValidation.Decode(new DurableRecord(3, bytes), new string('b', 64)));
        var malformed = JsonNode.Parse(bytes)!;
        Assert.True(malformed["value"]!.AsObject().Remove("sources"));
        Assert.Throws<StorageUnavailableException>(() => ScorePersistenceValidation.Decode(
            new DurableRecord(3, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(malformed)), Owner));
        var unknown = JsonNode.Parse(bytes)!;
        unknown["value"]!["undeclared"] = true;
        Assert.Throws<StorageUnavailableException>(() => ScorePersistenceValidation.Decode(
            new DurableRecord(3, System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(unknown)), Owner));
    }

    [Fact]
    public void CapacityAndDuplicateProjectionIdentityFailClosedWithoutEviction()
    {
        var state = ScoreState.Empty(Owner);
        var projection = ScoreProjectionState.From(ScorePersistenceContext.ReadSource(Source(0.1m)));
        for (var i = 0; i < 1000; i++) { state.Projections.Add(projection with { Id = "synthetic-record-" + i }); }
        Assert.NotEmpty(ScorePersistenceValidation.Encode(state));
        state.Projections.Add(projection with { Id = "synthetic-record-1000" });
        Assert.Throws<StorageUnavailableException>(() => ScorePersistenceValidation.Encode(state));
        Assert.Equal(1001, state.Projections.Count);
        state.Projections.RemoveAt(1000);
        state.Projections[999] = state.Projections[0];
        Assert.Throws<StorageUnavailableException>(() => ScorePersistenceValidation.Encode(state));
    }

    [Fact]
    public void SerializedCapacityAlsoFailsClosedBeforeCollectionLimits()
    {
        var state = ScoreState.Empty(Owner);
        var calculation = new FinancialScoreCalculator().Calculate("synthetic-id", "score-default-source-synthetic", Owner,
            "USD", [], FinancialScoreProfileSettings.Unconfigured, At);
        var dto = ScoreCalculationState.From(calculation with
        { Factors = calculation.Factors.Select(f => f with { Explanation = new string('s', 500) }).ToArray() });
        for (var i = 0; i < 400; i++) { state.Calculations.Add(dto with { Id = "synthetic-id-" + i, Source = "score-default-source-" + i }); }
        Assert.Throws<StorageUnavailableException>(() => ScorePersistenceValidation.Encode(state));
        Assert.Equal(400, state.Calculations.Count);
    }

    private static IntegrationEventEnvelope<FinancialRecordChangedV1> Source(decimal amount) =>
        new("synthetic-event", "synthetic-occurrence", FinancialRecordEventTypes.ExpenseCreated, At, "expense-service", 1,
            "synthetic-correlation", "synthetic-cause", Owner,
            new FinancialRecordChangedV1("synthetic-record", amount, "USD", "expense.food", new DateOnly(2026, 10, 21), "active", 0, "manual", At));
}
