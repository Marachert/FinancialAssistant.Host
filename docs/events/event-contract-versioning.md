# Integration Event Contract Versioning

Related Jira: FIN-64.

This guide defines naming, schema versioning, compatibility, and breaking-change
rules for shared integration events. It complements
[RabbitMQ Topology and Delivery Conventions](rabbitmq-topology.md), which defines
broker topology and delivery behavior.

The service that owns the authoritative state change owns the event intent and
contract. Shared contract packages provide consistent envelope and schema types;
they do not create shared data ownership or make RabbitMQ a source of truth.

## Event Name

Every integration event type and RabbitMQ routing key uses:

```text
{domain}.{action}.v{schemaVersion}
```

Variable segments use lowercase ASCII letters, digits, and single hyphens.
`schemaVersion` is a positive integer major compatibility version. Examples:

```text
user.registered.v1
transaction.confirmed.v1
income.created.v1
expense.created.v1
score.calculated.v1
```

The complete event type is the RabbitMQ routing key. Do not add environment,
tenant, user, account, merchant, or other personal or financial identifiers to
an event name or routing key.

## Envelope and Version Alignment

Every event envelope carries `eventId`, `occurrenceId`, `eventType`, and numeric
`schemaVersion`. `eventId` uniquely identifies one serialized message.
`occurrenceId` is an opaque identifier assigned once to the authoritative business
occurrence and reused only by schema-version variants of that occurrence. It must
not encode a user, account, transaction, merchant, or other personal or financial
identifier.

The event-type suffix and schema-version field must match:

| Event type | Required schema version |
| --- | ---: |
| `user.registered.v1` | `1` |
| `transaction.confirmed.v1` | `1` |
| `income.created.v2` | `2` |
| `expense.created.v3` | `3` |

A mismatch is an invalid contract and must not be published or processed.
`schemaVersion` represents the complete serialized contract: shared envelope
requirements plus the event payload. This baseline has no minor version in the
routing key.

Published messages are immutable facts. Never rewrite stored outbox messages or
broker payloads to look like a newer contract version.

## Backward-Compatible Changes

A change may remain on the existing major version only when every conforming
consumer of that version can continue processing old and new messages without a
deployment.

Allowed changes are limited to:

- adding an optional payload field with a documented absence/default behavior;
- adding optional metadata that does not alter existing field meaning;
- clarifying documentation without changing serialized values or semantics;
- relaxing a producer-side validation limit when all previously valid values
  retain the same meaning and consumers already accept the wider range.

Consumers must ignore unknown fields, apply documented defaults for absent
optional fields, and continue accepting messages produced before an additive
change. Producers must keep emitting every existing required field.

An additive field is not compatible when a consumer must use it to preserve
correctness, authorization, privacy, financial meaning, or deterministic
calculation. That requirement creates a new major version.

## Breaking Changes

The following changes require a new major `schemaVersion` and routing key:

- removing or renaming a field;
- changing a field type, format, unit, precision, or nullability;
- making an optional field required;
- changing a field's business meaning or source-of-truth semantics;
- changing identity, correlation, causation, idempotency, or ordering semantics;
- splitting or combining events in a way that changes consumer behavior;
- adding a value to a closed enum or changing an existing enum value;
- increasing payload privacy or authorization requirements;
- changing the required shared envelope shape;
- requiring consumers to calculate a different financial result from the same
  serialized values.

Never silently publish a breaking payload behind an existing routing key.

## Breaking-Change Process

A publisher introducing a breaking contract must:

1. document the new contract and compatibility reason before implementation;
2. create the new event type, schema, tests, and routing key, such as
   `transaction.confirmed.v2`;
3. inventory authorized consumers and agree on a migration and deprecation
   window;
4. deploy consumers able to bind and process the new version idempotently;
5. dual-publish old and new versions from the same authoritative business
   occurrence when existing consumers still require the old version;
6. monitor publish confirms, unroutable messages, consumer failures, retries,
   dead letters, and version-specific adoption without logging payloads;
7. stop the old version only after every required consumer has migrated and the
   agreed support window has ended;
8. remove old bindings and code in a later change while retaining the historical
   schema and deprecation record.

Dual-published versions are separate serialized messages and receive distinct
`eventId` values, but they must carry the same `occurrenceId`. They may also share
safe `correlationId` and `causationId` values for tracing; correlation and
causation are not deduplication identities. A consumer bound to more than one
version of the same event must deduplicate cross-version business side effects by
`occurrenceId` before applying any version. Inbox handling still deduplicates
broker redelivery of an individual serialized message by `eventId`. A consumer
must subscribe only to versions it can process.

A breaking migration must be reversible during the support window. Turning off
the old version is a deliberate release decision, not an automatic consequence
of publishing the new version.

## Consumer Compatibility Rules

Consumers:

- reject an unsupported major version using a safe terminal reason code;
- never deserialize a newer major version into an older type;
- tolerate unknown fields within a supported major version;
- treat missing optional fields according to the documented default;
- validate event type and `schemaVersion` alignment before payload handling;
- deduplicate at-least-once redelivery of each serialized message by `eventId`;
- when subscribed to multiple versions of an event, deduplicate the authoritative
  business occurrence by `occurrenceId` before producing side effects;
- keep deterministic business validation authoritative;
- do not infer authorization or ownership from routing alone.

Unsupported versions and invalid contracts are terminal failures, not transient
retries. They follow the owning queue's dead-letter policy.

## Required Examples

These examples describe synthetic contract shapes, not real broker payloads.

### user.registered.v1

Owner: Identity Service.

Required payload fields:

```text
userId
authenticationMethod
```

Optional additive example: `registrationChannel`, when consumers document a
safe `unknown` default. Breaking example: replacing `userId` with an email
address or changing its identity semantics.

### transaction.confirmed.v1

Owner: Transaction Intake Service.

Required payload fields:

```text
transactionId
userId
draftId
transactionType
amount
currency
categoryId
date
confirmedAtUtc
```

Optional additive example: `merchant`, with absent meaning not supplied.
Breaking example: changing `amount` from a decimal currency value to integer
minor units or changing the date format.

### income.created.v1

Owner: Income Service.

Required payload fields:

```text
incomeId
userId
amount
currency
categoryId
date
createdAtUtc
```

Optional additive example: `sourceTransactionId`, with absent meaning not
linked to an intake confirmation. Breaking example: changing amount precision,
currency semantics, or the authoritative income identity.

### expense.created.v1

Owner: Expense Service.

Required payload fields:

```text
expenseId
userId
amount
currency
categoryId
date
createdAtUtc
```

Optional additive example: `merchant`, with absent meaning not supplied.
Breaking example: making merchant required, changing category identity
semantics, or changing amount units.

## Financial Record Lifecycle Events

FIN-99 implements the Income- and Expense-owned lifecycle families:

```text
income.created.v1
income.updated.v1
income.archived.v1
income.restored.v1
expense.created.v1
expense.updated.v1
expense.archived.v1
expense.restored.v1
```

All use `IntegrationEventEnvelope<FinancialRecordChangedV1>` with schema version
`1`. The payload fields are `recordId`, `amount`, `currency`, `categoryId`,
`date`, `status`, `revision`, `origin`, and `changedAtUtc`. The envelope's
`userIdHash` is a SHA-256 pseudonymous identifier; raw user identifiers and
merchant text are not published. Correlation follows the originating trace, and
confirmed records use the upstream confirmation event as causation.

One record revision produces one deterministic event identity, making confirmed
event replay idempotent. Development uses a service-owned in-memory outbox. A
durable implementation must atomically commit the financial record and outbox row,
then retry transient RabbitMQ publish failures with bounded backoff. Observability
uses safe reason codes and identifiers only, never event payloads. Terminal
contract or routing failures follow the owning queue's dead-letter policy.

## Privacy and Review

### score.calculated.v1

Owner: Financial Score Service.

The schema-version-1 payload contains `calculationId`, `currency`, integer `score`, `formulaVersion`, bounded factor code/contribution pairs, and `calculatedAtUtc`. The envelope carries the pseudonymous `userIdHash`. The contract does not contain source record IDs, raw identity, merchant/category text, OCR content, prompts, or semantic-provider text. The calculation ID is deterministic for the source event so an at-least-once republish remains deduplicable.

Changing the formula does not silently reinterpret history: a formula change requires a new stored `formulaVersion`, documentation, and compatibility review. A payload-breaking change requires a new event schema version and routing key.

### analytics.updated.v1

Owner: Analytics Service. The payload contains currency, reference date,
monthly income and expense totals, optional daily expense limit, daily expense
spent, optional top expense category identifier, and update timestamp. The
envelope carries only the pseudonymous owner hash.

### recommendation.generated.v1

Owner: Recommendation Service. The payload contains a stable recommendation
identifier, currency, deterministic code and severity, bounded wording, numeric
fact code/value pairs, and generation timestamp. Optional AI wording never
becomes a fact and cannot alter codes, severity, or values.

### notification.prepared.v1

Owner: Notification Service. The payload contains stable notification and
recommendation identifiers, currency, channel, versioned template code,
delivery status, and preparation timestamp. Device tokens, endpoints, raw
identities, and provider credentials are excluded.

Contract examples use synthetic identifiers only. Event schemas and examples
must not include credentials, tokens, passwords, raw receipt images, raw OCR
text, unrestricted LLM content, account numbers, or unnecessary personal or
financial details.

Every new event or major version requires:

- an owning producer and related Jira issue;
- schema and serialization tests;
- event type and schema-version alignment tests;
- compatibility classification;
- payload privacy review;
- consumer and routing documentation;
- deprecation evidence when replacing an older version.

FIN-65 owns the shared integration event envelope implementation. That
implementation must enforce the alignment and required envelope fields defined
here without weakening service-owned validation.
