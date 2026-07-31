using FinancialAssistant.Shared.Contracts.Events;
using Xunit;

namespace FinancialAssistant.Shared.Contracts.Tests;

public sealed class IntegrationEventEnvelopeTests
{
    [Fact]
    public void Constructor_PreservesTypedPayloadAndCanonicalMetadata()
    {
        var payload = new TestPayload("transaction-001", 42.50m);
        var occurredAt = new DateTimeOffset(2026, 7, 31, 12, 15, 0, TimeSpan.FromHours(3));

        var envelope = Create(occurredAtUtc: occurredAt, payload: payload);

        Assert.Equal("event-001", envelope.EventId);
        Assert.Equal("occurrence-001", envelope.OccurrenceId);
        Assert.Equal("transaction.confirmed.v1", envelope.EventType);
        Assert.Equal(DateTimeOffset.Parse("2026-07-31T09:15:00+00:00"), envelope.OccurredAtUtc);
        Assert.Equal(TimeSpan.Zero, envelope.OccurredAtUtc.Offset);
        Assert.Equal("transaction-intake", envelope.Producer);
        Assert.Equal(1, envelope.SchemaVersion);
        Assert.Equal("correlation-001", envelope.CorrelationId);
        Assert.Equal("causation-001", envelope.CausationId);
        Assert.Equal("synthetic-user-hash", envelope.UserIdHash);
        Assert.Same(payload, envelope.Payload);
    }

    [Fact]
    public void Constructor_AllowsNoUserHash()
    {
        var envelope = Create(userIdHash: null);

        Assert.Null(envelope.UserIdHash);
    }

    [Fact]
    public void Constructor_AllowsVersionVariantsToShareOccurrenceIdentity()
    {
        var firstVersion = Create(
            eventId: "event-v1",
            occurrenceId: "occurrence-shared",
            eventType: "transaction.confirmed.v1",
            schemaVersion: 1);
        var secondVersion = Create(
            eventId: "event-v2",
            occurrenceId: "occurrence-shared",
            eventType: "transaction.confirmed.v2",
            schemaVersion: 2);

        Assert.NotEqual(firstVersion.EventId, secondVersion.EventId);
        Assert.Equal(firstVersion.OccurrenceId, secondVersion.OccurrenceId);
    }

    [Theory]
    [InlineData("eventId")]
    [InlineData("occurrenceId")]
    [InlineData("eventType")]
    [InlineData("producer")]
    [InlineData("correlationId")]
    [InlineData("causationId")]
    public void Constructor_RejectsMissingRequiredMetadata(string parameterName)
    {
        Action act = parameterName switch
        {
            "eventId" => () => Create(eventId: " "),
            "occurrenceId" => () => Create(occurrenceId: " "),
            "eventType" => () => Create(eventType: " "),
            "producer" => () => Create(producer: " "),
            "correlationId" => () => Create(correlationId: " "),
            "causationId" => () => Create(causationId: " "),
            _ => throw new InvalidOperationException($"Unexpected parameter {parameterName}.")
        };

        var exception = Assert.Throws<ArgumentException>(act);

        Assert.Equal(parameterName, exception.ParamName);
    }

    [Theory]
    [InlineData("transaction.confirmed")]
    [InlineData("Transaction.confirmed.v1")]
    [InlineData("transaction..v1")]
    [InlineData("transaction.confirmed.v01")]
    [InlineData("transaction.confirmed.v0")]
    [InlineData("transaction.confirmed.vx")]
    [InlineData("transaction.confirmed.v1.extra")]
    [InlineData("transaction--intake.confirmed.v1")]
    public void Constructor_RejectsNonCanonicalEventType(string eventType)
    {
        var exception = Assert.Throws<ArgumentException>(() => Create(eventType: eventType));

        Assert.Equal("eventType", exception.ParamName);
    }

    [Fact]
    public void Constructor_RejectsEventTypeAndSchemaVersionMismatch()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Create(eventType: "transaction.confirmed.v2", schemaVersion: 1));

        Assert.Equal("schemaVersion", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveSchemaVersion(int schemaVersion)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => Create(schemaVersion: schemaVersion));

        Assert.Equal("schemaVersion", exception.ParamName);
    }

    [Fact]
    public void Constructor_RejectsBlankUserHash()
    {
        var exception = Assert.Throws<ArgumentException>(() => Create(userIdHash: " "));

        Assert.Equal("userIdHash", exception.ParamName);
    }

    [Fact]
    public void Constructor_RejectsNullPayload()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => new IntegrationEventEnvelope<TestPayload>(
                "event-001",
                "occurrence-001",
                "transaction.confirmed.v1",
                DateTimeOffset.UtcNow,
                "transaction-intake",
                1,
                "correlation-001",
                "causation-001",
                null,
                null!));

        Assert.Equal("payload", exception.ParamName);
    }

    private static IntegrationEventEnvelope<TestPayload> Create(
        string eventId = "event-001",
        string occurrenceId = "occurrence-001",
        string eventType = "transaction.confirmed.v1",
        DateTimeOffset? occurredAtUtc = null,
        string producer = "transaction-intake",
        int schemaVersion = 1,
        string correlationId = "correlation-001",
        string causationId = "causation-001",
        string? userIdHash = "synthetic-user-hash",
        TestPayload? payload = null) =>
        new(
            eventId,
            occurrenceId,
            eventType,
            occurredAtUtc ?? DateTimeOffset.UtcNow,
            producer,
            schemaVersion,
            correlationId,
            causationId,
            userIdHash,
            payload ?? new TestPayload("transaction-001", 42.50m));

    private sealed record TestPayload(string TransactionId, decimal Amount);
}
