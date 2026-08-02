# Financial Core End-to-End API Flow

Status: Accepted for the POC
Owner: Financial core services
Jira: FIN-104

## Purpose

This document defines the first end-to-end backend journey from authenticated user
input to a confirmed Income- or Expense-owned record and an updated Financial
Summary response. It is the shared implementation and integration-test contract
for backend and client teams.

The flow preserves the normative
[financial source-of-truth rules](financial-source-of-truth.md) and
[financial core service boundaries](financial-core-service-boundaries.md).
A draft, confirmation event, client response, broker message, or summary
projection is not an authoritative financial record. Authority begins only when
Income or Expense independently validates and commits its owned record.

## Scope and completion signal

The happy path covers:

1. create a draft from user input;
2. review and, when needed, correct the draft;
3. confirm one immutable draft revision;
4. create exactly one Income or Expense record;
5. publish the owning record lifecycle event;
6. project that event into Financial Summary;
7. read dashboard totals with explicit freshness.

The end-to-end completion signal is not the confirmation HTTP response. It is a
Financial Summary response for the requested owner, currency, timezone, and
reference date that contains the expected record contribution exactly once and
reports a projection checkpoint at or after the owning record event. Until then,
the client may show confirmation success together with a pending/stale dashboard.

## Actors and trust boundaries

| Actor | Responsibility |
| --- | --- |
| Client | Sends opaque idempotency and correlation identifiers, displays draft review state, confirms only user-approved values, and polls freshness without duplicating writes |
| Public API Gateway | Authenticates the caller, strips untrusted internal headers, injects trusted gateway authentication and owner context, routes requests, and applies perimeter policy |
| Transaction Intake | Owns idempotent draft creation, review, revision, rejection, confirmation intent, and the `transaction.confirmed.v1` outbox |
| Income or Expense | Independently validates the matching confirmation and owns the authoritative record, revision, lifecycle, inbox, and record-event outbox |
| Financial Summary | Idempotently projects Income/Expense lifecycle events and serves owner-hash/currency-scoped derived totals with freshness |
| RabbitMQ | Carries versioned events with at-least-once delivery; it is not a source of truth |

No step accepts a client-selected owner ID. No service reads another service's
storage. Test fixtures use synthetic users, categories, amounts, and identifiers.

## Preconditions

Before the journey:

- Identity has established an authenticated user and the gateway can produce a
  trusted owner context;
- Profile has default currency/timezone preferences or Intake can return an
  explicit missing-preference review state;
- Category has seeded stable income and expense categories;
- Intake, the matching Income/Expense consumer, and Financial Summary use the
  same supported v1 contracts;
- production adapters have durable service-owned stores, inboxes, and
  transactional outboxes;
- every public gateway route and downstream destination used below is active,
  including draft review/update/reject and Financial Summary, before a full
  public HTTP end-to-end test is classified green.

Public gateway readiness is an explicit prerequisite, not a current capability.
At FIN-104, the checked-in gateway catalog keeps Transaction Intake disabled,
marks only intake and confirmation placeholders, and has no review, update,
rejection, or Summary route. Service-contract tests may call the `/api/v1` routes
in an isolated Transaction Intake test host with synthetic trusted-gateway
headers. A public-gateway test must fail its prerequisite check until the complete
route set and destinations are activated by their owning delivery work.

The deterministic development parser and in-memory adapters are sufficient for
local and CI contract tests. No live or paid AI/OCR provider is required.

## Synchronous REST phase

### E2E-001: Create a draft

The service-contract request is:

```http
POST /api/v1/transactions/intake
Idempotency-Key: synthetic-opaque-key
X-Correlation-Id: synthetic-correlation-id
Content-Type: application/json

{
  "input": "synthetic expense statement"
}
```

The intended gateway alias is `POST /transactions/intake`; it becomes usable only
after the public gateway prerequisite above is satisfied. The gateway supplies
trusted authentication and owner headers downstream.

Transaction Intake normalizes the input, uses current Profile/Category reference
contracts where configured, invokes its parser adapter, and independently
validates every suggestion. It atomically stores the owner-scoped idempotency
fingerprint, draft, source reference, publication state, and draft revision.

The first successful request returns `201` with the reviewable draft. Repeating
the same owner, key, and normalized input returns `200` with the same draft ID
and revision. Reusing the key for different normalized input returns
`409 idempotency_key_conflict`. Invalid input returns
`400 invalid_transaction_input`. No result changes financial totals.

After local commit, `transaction.draft-created.v1` may be published for
asynchronous enrichment. It carries an opaque source reference and changes no
financial state.

### E2E-002: Review the current revision

The service-contract test reads:

```http
GET /api/v1/transactions/drafts/{draftId}
```

The intended gateway alias is `GET /transactions/drafts/{draftId}`. The response is
owner-scoped and returns the current values, status, revision, confidence,
ambiguities, and `requiresReview`. Another owner's draft is indistinguishable
from a missing draft and returns `404 transaction_draft_not_found`.

The client must render returned values for explicit review. It must not infer
confirmation from parser confidence or silently fill missing values.

### E2E-003: Correct and revalidate when needed

For an ambiguous, incomplete, low-confidence, or user-corrected draft, the
service-contract test sends the full reviewed replacement:

```http
PUT /api/v1/transactions/drafts/{draftId}
Content-Type: application/json

{
  "expectedRevision": 0,
  "type": "expense",
  "amount": 25.50,
  "currency": "USD",
  "categoryId": "expense.groceries",
  "merchant": "Synthetic Market",
  "date": "2026-08-02",
  "note": null
}
```

`expectedRevision` comes from the reviewed response. Transaction Intake rejects a
missing or negative value, deterministically normalizes and revalidates the
complete replacement, and conditionally advances that exact revision. A stale
concurrent mutation returns `409 transaction_draft_not_editable`; the service does
not retry stale client values against the newer revision. Invalid suggestions
remain review-required and cannot be confirmed.

A user may instead send
`POST /api/v1/transactions/drafts/{draftId}/reject`. Rejection is idempotent,
terminal, publishes no confirmation event, and changes no financial total.

### E2E-004: Confirm one reviewed revision

The service-contract test sends:

```http
POST /api/v1/transactions/drafts/{draftId}/confirm
X-Correlation-Id: synthetic-correlation-id
```

The gateway alias is
`POST /transactions/drafts/{draftId}/confirm`. Intake verifies owner, draft
status, complete values, supported income/expense type, and absence of unresolved
review requirements. It atomically claims the current revision, assigns one
stable transaction ID, records confirmation, and writes one
`transaction.confirmed.v1` outbox message.

The first successful confirmation returns `201` with the stable transaction
identity. Repeating or racing the same confirmation returns `200` with the same
identity and creates no additional message or record. Unknown, transfer,
incomplete, review-required, or rejected drafts return
`422 transaction_draft_not_confirmable`.

The response proves that Intake accepted confirmation intent. In production it
does not promise that the authoritative record or Summary projection is already
visible.

## Asynchronous event phase

### E2E-005: Deliver confirmation to the matching owner

The Intake dispatcher publishes `transaction.confirmed.v1` after its outbox row
is committed. RabbitMQ may redeliver the message.

Exactly the consumer matching `transactionType` processes the event:

- `income` routes to Income;
- `expense` routes to Expense;
- every other type is rejected as an invalid contract and creates no record.

The consumer validates owner, transaction and draft identifiers, positive amount,
supported currency, category namespace, date, type, and contract version. It
deduplicates by event ID and stable transaction identity.

### E2E-006: Commit one authoritative record

The matching Income or Expense service atomically commits:

- one owner-scoped authoritative active record with
  `origin = confirmed_transaction`;
- one inbox/business-idempotency marker;
- one service-owned lifecycle outbox message.

The record is the first point at which financial calculations may change. A
duplicate confirmation event returns the existing record outcome and contributes
exactly once.

### E2E-007: Publish the owning lifecycle event

The owning dispatcher publishes exactly one revision-1 event:

- `income.created.v1`; or
- `expense.created.v1`.

The envelope carries safe correlation and causation identifiers plus a
pseudonymous owner hash. The payload carries record ID, amount, currency,
category, date, active status, revision, origin, and change time. Raw intake,
merchant, note, draft ID, idempotency key, raw owner identity, receipt, OCR, and
prompt content are excluded.

### E2E-008: Apply the Summary projection

Financial Summary validates the envelope and applies the lifecycle event by
owner hash, record type, record ID, and revision. The same or older revision is a
no-op. A newer revision replaces the prior contribution. Active records
contribute once to daily, Monday-based weekly, and calendar-month totals for
their currency; unlike currencies are never combined.

The projection records its event/checkpoint time. A failed or partial rebuild is
not published as fresh.

## Synchronous dashboard read

### E2E-009: Query the latest summary

The service-contract request is:

```http
GET /api/v1/financial-summary?currency=USD&timeZoneId=Europe%2FKyiv&referenceDate=2026-08-02
```

The intended gateway alias is `GET /financial-summary`; it becomes usable only
after the public gateway prerequisite above is satisfied. A valid query returns
`200` with daily, weekly, monthly, category, balance-delta, and freshness fields.

If the record event has not arrived, the response may contain previous or zero
values with `freshness.isStale = true`. The service does not synchronously query
Income or Expense to hide lag. The client polls with bounded exponential backoff
and jitter, observes freshness/checkpoint progress, and stops at the test or UX
timeout. Tests must not use a fixed sleep as proof of convergence.

### E2E-010: Verify convergence

The journey is converged when:

- the expected currency bucket includes the synthetic amount exactly once in the
  correct income or expense side;
- `balanceDelta` reflects income minus expense;
- the expected category bucket includes the contribution;
- daily/weekly/monthly inclusion matches the record date and requested timezone;
- `freshness.isStale = false`;
- `lastEventAtUtc` is at or after the created lifecycle event time.

The client does not require cross-currency conversion and never calculates an
authoritative replacement total locally.

## Execution profiles

### Production profile

Production uses durable stores and RabbitMQ:

```text
Client -> Gateway -> Intake REST
Intake outbox -> transaction.confirmed.v1 -> Income OR Expense inbox/store/outbox
Income/Expense outbox -> *.created.v1 -> Summary inbox/projection
Client -> Gateway -> Summary REST (poll until checkpoint or timeout)
```

An HTTP request ends at its owning service boundary. Downstream failure never
causes a distributed rollback of already committed producer state.

### Development and CI profile

The current Transaction Intake host may invoke independently validating
Income/Expense consumers in process through the same event contract, and the
summary projector may run in the test process. This profile may reduce transport
latency but must preserve:

- separate service-owned stores and interfaces;
- the same serialized contract validation;
- the same event and business idempotency keys;
- the same owner isolation and source-of-truth transition;
- explicit projection freshness;
- no direct test mutation or assertion against private service storage.

A test that bypasses confirmation or directly inserts a Summary row is not an
end-to-end financial-core test.

## Idempotency and retry ledger

| Boundary | Stable identity | Duplicate behavior | Retry owner |
| --- | --- | --- | --- |
| Draft creation | authenticated owner + opaque idempotency key + normalized-input fingerprint | Same input returns same draft; different input returns 409 | Client retries same request/key after transport failure |
| Draft update/reject | owner + draft ID + expected/current revision and status | One atomic mutation wins; stale update returns 409; rejection is status-idempotent | Client reloads current draft before another update |
| Confirmation | owner + draft ID + claimed revision | Same stable transaction and response; one confirmation outbox row | Client retries confirmation; Intake resumes pending publication |
| Confirmation consumption | event ID + transaction ID | One Income/Expense record and one created-event outbox row | Matching consumer inbox retries transient failures |
| Record event publication | event type + record ID + revision | One logical lifecycle event | Owning service outbox dispatcher |
| Summary projection | owner hash + record type + record ID + revision | Same/older revision is a no-op | Summary consumer inbox retries transient failures |
| Summary read | owner + currency + timezone + reference date | Read-only; no side effect | Client polls boundedly when stale/unavailable |

Retries use bounded exponential backoff and jitter. Invalid contracts, unsupported
versions, ownership violations, and privacy violations are terminal and use safe
reason codes. Payloads are never logged.

## Failure matrix

| Failure point | Required observable result | State and recovery |
| --- | --- | --- |
| Missing/invalid gateway authentication | Stable `401`; no service state change | Client reauthenticates; never retries with caller-supplied trusted headers |
| Invalid intake or missing idempotency key | Stable `400`; no draft | Client corrects request |
| Reused key with different input | `409 idempotency_key_conflict` | Original draft remains unchanged |
| Profile/Category reference dependency unavailable | Explicit unavailable result or review-required ambiguity | No guessed value becomes authoritative; retry dependency read |
| Missing or foreign draft | `404 transaction_draft_not_found` | No identity disclosure or state change |
| Stale draft mutation | `409 transaction_draft_not_editable` | Reload current revision |
| Draft not confirmable | `422 transaction_draft_not_confirmable` | Review/update or reject; no event/record/total |
| Intake outbox publish transient failure | Confirmation remains committed; Summary may be pending | Intake dispatcher retries; client confirmation replay returns same identity |
| Invalid confirmation event | No authoritative record; terminal safe reason/DLQ | Operator investigates contract; never synthesize a record |
| Income/Expense transient consumer failure | No partial record; message retried | Consumer inbox retry with idempotency |
| Record outbox publish failure | Authoritative record remains committed; Summary is stale | Owning dispatcher retries the same logical revision event |
| Duplicate/out-of-order lifecycle event | No duplicate or rollback to stale revision | Summary ignores same/older revision |
| Summary unavailable or stale | Explicit unavailable transport result or `200` with `isStale = true` | Client polls boundedly; no repeat confirmation |
| End-to-end timeout | Test reports last checkpoint and safe IDs, not payloads | Preserve state for retry/diagnosis; do not issue compensating financial writes |

## Integration-test scenarios

A full implementation must cover these stable scenarios with synthetic data:

| ID | Scenario | Required assertion |
| --- | --- | --- |
| FC-E2E-001 | New expense phrase | 201 draft, review, 201 confirmation, one Expense record event, one Summary contribution |
| FC-E2E-002 | New income phrase | Same flow through Income and positive balance delta |
| FC-E2E-003 | Ambiguous parser output | Review-required draft; 422 confirmation; zero total change |
| FC-E2E-004 | User correction | Revised values, category, date, and currency are the only values committed |
| FC-E2E-005 | Draft request replay | Same key/input returns same draft and publishes one logical draft-created event |
| FC-E2E-006 | Draft key conflict | Same key/different input returns 409 and preserves the original draft |
| FC-E2E-007 | Concurrent confirmation | One stable transaction, record, lifecycle event, and Summary contribution |
| FC-E2E-008 | Broker redelivery and out-of-order event | Duplicate and stale revisions are no-ops; latest revision wins |
| FC-E2E-009 | Record-event publish delay | Record is authoritative while Summary reports stale; later retry converges once |
| FC-E2E-010 | Owner isolation | Another owner sees 404/own totals and cannot observe the draft or contribution |
| FC-E2E-011 | Currency isolation | USD and EUR remain separate; no implicit conversion or combined total |
| FC-E2E-012 | Summary timeout | Bounded poll fails with checkpoint diagnostics and does not repeat financial writes |

The full HTTP test composes public contracts through the gateway or a faithful
gateway test harness. It may inspect safe responses, recorded event metadata, and
published contracts, but it must not read or mutate private service tables. It
uses the deterministic parser and in-memory/fake broker adapters unless a
separately approved environment test explicitly selects durable infrastructure.

## Correlation and diagnostics

One opaque correlation ID follows the request and downstream events. Causation
links confirmation to the created record event. Diagnostics may record route,
status code, event type, event ID, safe owner hash, record ID, revision, retry
count, lag, and checkpoint. They must not record raw input, merchant, note, amount,
credentials, receipt/OCR data, or provider prompts/responses.

On failure, an integration test reports:

- stable scenario ID;
- HTTP step and safe error code;
- event type and safe IDs where available;
- expected and observed revision/checkpoint;
- retry count and elapsed timeout;
- whether authoritative record creation had occurred.

## Related contracts

- `docs/architecture/financial-source-of-truth.md`
- `docs/architecture/financial-core-service-boundaries.md`
- `docs/engineering/transaction-intake-draft-flow.md`
- `docs/events/event-contract-versioning.md`
- `docs/engineering/financial-summary-read-model.md`
- `docs/api/financial-summary-v1.md`
- `backend/services/transaction-intake/README.md`
- `backend/services/income/README.md`
- `backend/services/expense/README.md`
- `backend/services/financial-summary/README.md`
