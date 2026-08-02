# Recommendations and Notifications v1

## Deterministic authority

Recommendation codes are generated from backend facts. The v1 rules cover a
configured daily expense limit being reached, monthly expenses approaching or
reaching confirmed income, a low or strong deterministic financial score, and
a non-invasive steady-course fallback.

Recommendation identifiers are stable hashes of pseudonymous owner scope,
currency, source event, and rule code. Event replay is idempotent.

`IRecommendationWordingProvider` can later provide bounded wording. It
receives an already-authoritative recommendation and can return only title and
body. Wording is rejected when blank, oversized, or control-character-bearing.
No provider is enabled in v1.

## Event flow

1. Recommendation Service consumes `analytics.updated.v1` or
   `score.calculated.v1`.
2. Accepted events update an owner/currency insight snapshot.
3. Deterministic rules replace the current recommendation set.
4. Each item publishes `recommendation.generated.v1`.
5. Notification Service prepares one push and one web notification.
6. Each accepted preparation publishes `notification.prepared.v1`.

RabbitMQ mode uses `fa.events`, a quorum application queue, publisher
confirms, delayed retries at 5 seconds, 30 seconds, and 5 minutes, and a
terminal DLQ. Malformed contracts go directly to the DLQ.

## Storage and delivery limitations

The checked-in POC store is process-local memory. It does not claim durable
history, an outbox, replica coordination, or restart recovery. Production work
must add service-owned durable stores and outboxes before external delivery is
enabled.

Push and web are provider-neutral preparations. No external provider, token,
device registration, or paid API is used. Provider credentials and raw endpoint
identifiers must never enter logs or event payloads.
