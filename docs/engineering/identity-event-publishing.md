# Identity Event Publishing Baseline

## Purpose

FIN-77 introduces the Identity Service integration-event publishing baseline for:

```text
user.registered.v1
user.signed_in.v1
authentication.failed.v1
token.revoked.v1
```

Identity Service remains authoritative for credentials, accounts, access issuance, refresh sessions, and revocation state. RabbitMQ transports facts after authoritative changes; it is never the source of truth.

## Shared event envelope

Every Identity integration event is emitted as:

```json
{
  "eventId": "uuid",
  "eventType": "user.registered.v1",
  "occurredAt": "2026-07-07T12:00:00Z",
  "publishedAt": "2026-07-07T12:00:01Z",
  "producer": "financial-assistant-identity-service",
  "schemaVersion": 1,
  "correlationId": "request-correlation-id",
  "causationId": "request-or-source-event-id",
  "userIdHash": "purpose-specific-hmac",
  "payload": {},
  "metadata": {
    "contentType": "application/json"
  }
}
```

Event names use `domain.action.v{version}`. The event type suffix and numeric schema version must align.

Compatibility note: `user.signed_in.v1` is an existing Identity event name, while
the newer shared event naming validator rejects underscores. This is an explicit
legacy exception/migration gap, not permission to rename a live v1 contract
without producer/consumer migration and regression coverage.

## Safe event catalog

### user.registered.v1

Payload:

```text
userId
authenticationMethod
```

The envelope carries a purpose-specific user hash. Email and credential data are excluded.

### user.signed_in.v1

Payload:

```text
userId
sessionId
authenticationMethod
```

No access token, refresh token, email, client identifier, or password data is published.

### authentication.failed.v1

Payload:

```text
authenticationMethod
reasonCode
```

This metric event intentionally has no subject identifier. Unknown email and wrong password produce the same safe event shape and reason code.

### token.revoked.v1

Payload:

```text
userId
sessionId
reason
```

Current reasons include `logout` and `refresh_reuse`.

## Synchronous flow

```text
client -> gateway -> Identity API -> deterministic Identity logic
       -> authoritative state change -> enqueue event intent -> REST response
```

Application services depend only on `IIdentityEventPublisher`. The active publisher converts the intent to the shared envelope and enqueues it in `IIdentityEventOutbox`. Application code never performs a direct fire-and-forget RabbitMQ publish.

## Asynchronous flow

```text
Identity outbox -> IdentityOutboxDispatcher -> IIdentityEventTransport
                -> RabbitMQ fa.events exchange -> consumer queues
```

The routing key is the complete event type. RabbitMQ messages are persistent, mandatory, and published on a channel with publisher confirmations and confirmation tracking enabled.

## Failure handling

The dispatcher:

1. reads pending outbox messages in bounded batches;
2. publishes one message at a time through the transport;
3. marks the message published only after transport success;
4. records a safe error code after failure;
5. schedules exponential retry with a configured maximum delay;
6. logs only event ID and event type.

RabbitMQ connection recovery does not replace the outbox. Messages remain pending until a confirmed publish succeeds.

## Configuration

```text
Identity:Events:Mode
Identity:Events:Exchange
Identity:Events:ConnectionString
Identity:Events:UserIdHmacKey
Identity:Events:BatchSize
Identity:Events:DispatchIntervalMilliseconds
Identity:Events:MaximumRetryDelaySeconds
```

Modes:

```text
InMemoryDevelopment
RabbitMq
```

`ConnectionString` and `UserIdHmacKey` are secrets and must come from environment variables or a secret manager. They must never be committed.

## Reliability boundary

FIN-77 removes direct fire-and-forget publication and establishes an outbox-compatible application boundary, retry dispatcher, and publisher-confirmed RabbitMQ transport.

The current active outbox adapter is `InMemoryIdentityEventOutbox`. Therefore this increment does not claim crash durability:

- pending events are lost on process restart;
- pending events are not shared between replicas;
- authoritative state mutation and outbox enqueue are not yet one durable Elasticsearch operation;
- a process failure between state mutation and enqueue remains a known gap.

The durable adapter must implement `IIdentityEventOutbox` and coordinate state
plus event intent using the service-owned persistence design. The preferred
target is a PostgreSQL transaction over owned state and outbox intent; it is not
implemented by this documentation change. Any retained Elasticsearch design must
explicitly close its cross-document state-to-intent gap through a documented,
tested mechanism. Neither the current in-memory adapter nor an unimplemented
design may be described as a durable transaction. See
[storage policy](../architecture/storage-policy.md).

## Verification

Automated tests cover:

- shared envelope fields;
- event and schema version alignment;
- purpose-specific user hashing;
- absence of email and password fields;
- registration event;
- sign-in success event;
- indistinguishable sign-in failure metric events;
- session revocation event continuity;
- pending outbox behavior;
- retry after transport failure;
- marking a message published after successful transport delivery.
