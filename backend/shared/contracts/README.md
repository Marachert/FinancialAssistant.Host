# Shared Contracts

Stable integration contracts shared by backend services.

## Projects

```text
FinancialAssistant.Shared.Contracts/        Runtime contract library
FinancialAssistant.Shared.Contracts.Tests/  Deterministic contract tests
```

The runtime library contains technical integration types only. It does not own
service business rules, persistence models, or financial calculations.

## Integration Event Envelope

`IntegrationEventEnvelope<TPayload>` provides a generic, immutable envelope for
versioned integration-event payloads.

| Field | Contract |
| --- | --- |
| `EventId` | Unique identity of one serialized message; used for redelivery deduplication |
| `OccurrenceId` | Opaque identity shared by version variants of one authoritative occurrence |
| `EventType` | Canonical `{domain}.{action}.v{schemaVersion}` routing key |
| `OccurredAtUtc` | Authoritative occurrence time normalized to UTC |
| `Producer` | Stable service identifier, not a host or environment name |
| `SchemaVersion` | Positive major version matching the event-type suffix |
| `CorrelationId` | Bounded operational trace identity |
| `CausationId` | Identity of the command or event that caused this event |
| `UserIdHash` | Optional opaque, non-reversible user hash; never a raw user identifier |
| `Payload` | Strongly typed service-owned event payload |

The constructor rejects missing required metadata, null payloads, non-canonical
event names, non-positive schema versions, and event-type/schema mismatches.
It does not calculate user hashes or create event identities; producers remain
responsible for generating those values safely.

Synthetic example:

```csharp
using FinancialAssistant.Shared.Contracts.Events;

var envelope = new IntegrationEventEnvelope<TransactionConfirmedV1>(
    eventId: "event-001",
    occurrenceId: "occurrence-001",
    eventType: "transaction.confirmed.v1",
    occurredAtUtc: DateTimeOffset.UtcNow,
    producer: "transaction-intake",
    schemaVersion: 1,
    correlationId: "correlation-001",
    causationId: "causation-001",
    userIdHash: "synthetic-user-hash",
    payload: new TransactionConfirmedV1("transaction-001", 42.50m, "USD"));

public sealed record TransactionConfirmedV1(
    string TransactionId,
    decimal Amount,
    string Currency);
```

See [Integration Event Contract Versioning](../../../docs/events/event-contract-versioning.md)
for naming, compatibility, dual-publish, idempotency, and privacy rules.

## Rules

- contracts are versioned;
- contracts are not persistence models;
- contracts do not create shared data ownership;
- payloads remain owned and validated by the producing service;
- credentials, raw user identifiers, raw OCR text, and unrestricted LLM content
  must not enter shared envelopes.
