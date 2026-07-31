# RabbitMQ Topology and Delivery Conventions

Related Jira: FIN-62.

This guide defines the baseline RabbitMQ topology for local and PoC
environments. It applies to integration events that cross service ownership
boundaries. Backend services remain authoritative for their own state and
deterministic financial rules; RabbitMQ transports facts and is never a source
of truth.

## Environment Boundary

Each environment uses a separate RabbitMQ virtual host:

```text
fa-{environment}
```

Examples are `fa-local`, `fa-dev`, `fa-test`, `fa-staging`, and
`fa-prod`. The local Docker Compose stack already uses `fa-local`.

Exchange and queue names do not include an environment segment because the
virtual host is the isolation boundary. Production credentials, permissions,
and virtual hosts must never be shared with local or test environments.

## Segment Grammar

Variable name segments use lowercase ASCII letters, digits, and single hyphens:

```text
[a-z0-9]+(?:-[a-z0-9]+)*
```

Do not put tenant names, user identifiers, email addresses, account numbers,
merchant names, or other personal or financial data in exchanges, queues,
routing keys, headers, or operational logs.

## Exchanges

Every virtual host declares these durable exchanges:

| Exchange | Type | Purpose |
| --- | --- | --- |
| `fa.events` | `topic` | Normal integration-event delivery |
| `fa.retry` | `direct` | Consumer-owned bounded-delay retry queues |
| `fa.dead-letter` | `direct` | Terminal failed-delivery routing |

Exchanges are long-lived platform topology. Services must not create a second
domain-specific event exchange when `fa.events` and a versioned routing key
express the contract.

Publishers send persistent messages to `fa.events` with publisher confirms
enabled and the mandatory flag set. A publish is successful only after the
broker confirms it. An unroutable mandatory publish is a failure and remains
eligible for outbox retry.

## Event Names and Routing Keys

Integration event types use:

```text
{domain}.{action}.v{schemaVersion}
```

The routing key is the complete event type, unchanged. Examples:

```text
user.registered.v1
transaction.confirmed.v1
token.revoked.v1
```

A consumer binds its queue to exact event types by default. Topic wildcards are
allowed only when the consumer owns and validates every matching contract.
Bindings such as `#` or `*.#` are forbidden for application queues.

The event type suffix and envelope `schemaVersion` must agree. A breaking
contract creates a new routing key version; it does not silently change the
payload behind an existing key.

## Queue Names and Ownership

Application queues are consumer-owned and use:

```text
fa.{consumer}.{purpose}.v{consumerContractVersion}
```

Examples:

```text
fa.profile.identity-lifecycle.v1
fa.income.transaction-confirmed.v1
fa.expense.transaction-confirmed.v1
fa.analytics.financial-events.v1
```

The consumer chooses the queue name, bindings, concurrency, retention, retry
budget, and dead-letter handling. Publishers know event contracts and routing
keys, not consumer queue names.

Application queues are durable. Production and PoC application queues use
quorum queues unless a documented capacity test justifies another type.
Auto-delete and exclusive application queues are forbidden. Every queue has one
owning service; multiple replicas of that service may compete on the same
queue.

## Binding Examples

| Publisher event | Exchange | Routing key | Consumer queue |
| --- | --- | --- | --- |
| Identity account created | `fa.events` | `user.registered.v1` | `fa.profile.identity-lifecycle.v1` |
| Transaction confirmed | `fa.events` | `transaction.confirmed.v1` | `fa.income.transaction-confirmed.v1` |
| Transaction confirmed | `fa.events` | `transaction.confirmed.v1` | `fa.expense.transaction-confirmed.v1` |
| Transaction confirmed | `fa.events` | `transaction.confirmed.v1` | `fa.analytics.financial-events.v1` |
| Token revoked | `fa.events` | `token.revoked.v1` | `fa.audit.identity-events.v1` |

Income and Expense consumers validate the confirmed transaction type before
changing service-owned state. A routing match never replaces contract,
authorization, or deterministic business validation.

## Delivery and Acknowledgement

The baseline is at-least-once delivery. Exactly-once delivery is not claimed.

A consumer:

1. validates the envelope, supported event type, schema version, and payload;
2. reserves the `eventId` in its durable inbox or equivalent idempotency store;
3. applies the deterministic service-owned state change;
4. commits the inbox result and state according to the service persistence
   design;
5. acknowledges the RabbitMQ delivery only after the durable result succeeds.

Duplicate `eventId` values return the previously recorded inbox outcome and
are acknowledged without repeating the state change. A consumer must not
acknowledge before its durable work succeeds.

There is no global ordering guarantee. A service that requires per-aggregate
ordering must carry and validate an aggregate sequence or version in that
specific event contract.

## Retry Policy

Retries are consumer-owned, bounded, and reserved for transient failures.
Validation, authorization, unsupported-version, and deterministic business-rule
failures are terminal and go directly to dead-letter handling.

The PoC baseline allows three delayed retries:

| Attempt | Delay | Retry queue suffix |
| ---: | ---: | --- |
| 1 | 5 seconds | `.retry.5s` |
| 2 | 30 seconds | `.retry.30s` |
| 3 | 5 minutes | `.retry.5m` |

A retry queue is named from the owning application queue, for example:

```text
fa.analytics.financial-events.v1.retry.5s
```

Each retry queue binds to `fa.retry` with its own full queue name as the direct
routing key. It has a fixed message TTL and dead-letters expired messages back
to `fa.events` with the original event routing key. Use one retry queue per
application queue, delay, and event routing key so the return route is explicit.

The consumer records a bounded retry-attempt header and republishes to the
selected retry route using publisher confirms before acknowledging the failed
delivery. Connection recovery does not replace this rule. Never use immediate
requeue loops, unbounded retries, or process-local sleep as a retry mechanism.

## Dead-Letter Policy

Every application queue has one terminal dead-letter queue:

```text
{applicationQueue}.dead-letter
```

Example:

```text
fa.analytics.financial-events.v1.dead-letter
```

The dead-letter queue binds to `fa.dead-letter` with the application queue name
as its direct routing key. Messages enter it after the retry budget is exhausted
or immediately for a terminal failure.

Dead-letter messages are not replayed automatically. Re-drive requires an
authorized operator action, a documented root-cause decision, validation
against the currently supported contract, and an audit record. Re-drive
preserves the original `eventId`; consumers still enforce inbox idempotency.

Metrics may include event type, owning consumer, safe reason code, attempt
count, and queue depth. Logs and alerts must not include message payloads,
credentials, tokens, real identities, receipt or OCR content, LLM content, or
financial values.

## Outbox and Inbox Expectations

A publisher writes event intent to a durable service-owned outbox as part of the
authoritative state-change design. The dispatcher publishes persistent,
mandatory messages with publisher confirms and marks an outbox item delivered
only after confirmation. A failed or unroutable publish remains pending for a
bounded, observable retry.

A consumer uses a durable inbox or equivalent idempotency record keyed by
`eventId`. Inbox retention must cover the maximum broker retention and
operator re-drive window. Outbox and inbox implementations are owned by each
service; shared libraries may provide contracts and low-level helpers but never
become a shared financial database.

In-memory outbox or inbox adapters are development aids only. They do not claim
crash durability or replica coordination.

## Local Example and Verification

The local virtual host is configured by
`infra/docker-compose/docker-compose.yml` as `fa-local`. Once a service
bootstrap declares topology, inspect it from `infra/docker-compose`:

```bash
docker compose exec rabbitmq rabbitmqctl -p fa-local list_exchanges name type durable
docker compose exec rabbitmq rabbitmqctl -p fa-local list_queues name durable arguments
docker compose exec rabbitmq rabbitmqctl -p fa-local list_bindings source_name destination_name routing_key
```

Expected topology includes `fa.events`, `fa.retry`, `fa.dead-letter`,
consumer-owned durable queues, exact versioned event bindings, bounded retry
queues, and one terminal dead-letter queue per application queue.

Use synthetic messages only. Never copy broker payloads from a real environment
into local development, tests, tickets, logs, or documentation.
