# Financial Summary Read Model Baseline

Related Jira: FIN-100.

## Purpose and ownership

The Financial Summary read model provides low-latency dashboard calculations from
authoritative Income and Expense lifecycle events. It is a derived projection,
not a write source of truth. Only Income and Expense may create or mutate the
financial records from which summary values are calculated.

The projection accepts confirmed authoritative records, including records created
manually through the owning service. It never consumes transaction drafts, parser
or LLM suggestions, raw receipt/OCR content, or unconfirmed financial data.

## Projection fields

Each latest-record projection stores only:

```text
recordType
recordId
userIdHash
amount
currency
categoryId
date
status
revision
origin
changedAtUtc
eventId
```

A query is scoped by pseudonymous user identifier, currency, and reference date.
Its result contains daily, weekly, and monthly `income`, `expense`, and
`balanceDelta` values; stable inclusive period boundaries; a category breakdown;
`lastEventAtUtc`; and `isStale`.

Daily means the reference date. Weekly means Monday through Sunday containing the
reference date. Monthly means the containing calendar month. The top-level balance
delta and category breakdown use the monthly period. Every total includes only
active records and is calculated independently per currency:

```text
income = sum(active income amounts in period)
expense = sum(active expense amounts in period)
balanceDelta = income - expense
```

## Event application

The projector accepts all eight `income.*.v1` and `expense.*.v1` lifecycle
events defined by FIN-99. It validates the shared envelope, version, pseudonymous
owner, record identifier, financial fields, revision, lifecycle status, and change
time before applying an event.

The latest projection is keyed by owner hash, record type, and record identifier.
A higher revision replaces an older revision. The same or an older revision is a
no-op, making broker redelivery and out-of-order replay deterministic. Archive
removes a record from totals without deleting its projection; restore includes its
newer active revision again.

## Rebuild

The source event log or durable service outboxes are the rebuild source. A rebuild:

1. creates an empty shadow projection generation;
2. replays the complete authorized event history in occurrence order;
3. applies revision idempotency exactly as live consumption does;
4. verifies expected event coverage, projection counts, per-currency checksums,
   rejected-event counts, and freshness;
5. atomically switches the read alias to the verified generation;
6. retains the previous generation for rollback during the release window.

The in-memory development adapter performs reset and deterministic replay directly.
It is not a production durability mechanism.

## Stale and failure behavior

Every query receives a freshness threshold. A projection is stale when no event has
ever been applied or when `asOfUtc - lastEventAtUtc` exceeds that threshold.
Stale results remain readable with explicit metadata so clients can distinguish
zero, last-known, and fresh values. Financial Summary does not synchronously call
Income or Expense to conceal lag.

Transient consumption failures are retried by the owning queue policy. Invalid
contracts and unsupported versions are terminal and dead-lettered with safe reason
codes. Logs and metrics may include event type, event ID, revision, lag, and safe
owner hash; they must not include event payloads or raw identities.

## Deferred API

FIN-101 defines mobile/web response contracts, empty-state serialization, timezone
inputs, and HTTP routes. FIN-100 establishes the internal projection behavior only.
