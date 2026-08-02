# Financial Source-of-Truth Rules

Status: Accepted for the POC  
Owner: Financial core services  
Jira: FIN-98

## Purpose

This document defines which records may affect balances, reports, limits, scores,
and recommendations. These rules are normative. Service-specific documentation
may add validation details but must not weaken them.

## Terms

- **Authoritative record**: a validated Income- or Expense-owned record committed
  for one authenticated owner.
- **Confirmed transaction**: a reviewed Transaction Intake draft whose exact
  revision was atomically confirmed and accepted by the owning financial service.
- **Manual record**: a record submitted directly to an authenticated Income or
  Expense API and accepted by the same deterministic validation used for
  authoritative data.
- **Suggestion**: unconfirmed parser, AI, OCR, speech, import, or recommendation
  output. A suggestion is never an authoritative record.
- **Active record**: an authoritative record whose lifecycle status is `active`.
- **Archived record**: an authoritative record retained for audit but excluded
  from current financial calculations.
- **Derived result**: a balance, report, limit state, score, or recommendation
  calculated from authoritative records. It is not a new source of truth.

## Normative Rules

### FST-001: Service-owned records are authoritative

Only validated records committed by Income Service or Expense Service may feed
financial calculations. Each service owns its records and persistence. Consumers
must use service APIs, versioned events, or a rebuildable projection; they must
not read another service's tables.

A record may be created from either:

1. a valid `transaction.confirmed.v1` event accepted idempotently by the
   matching Income or Expense consumer; or
2. a valid manual API command made for the authenticated owner.

The event, command, or draft is evidence of intent. The committed financial
record is the authority.

### FST-002: Suggestions cannot change financial state

Transaction drafts and raw or structured output from AI, OCR, speech
transcription, imports, or recommendation engines are suggestion-only. They
must not affect balances, reports, limits, scores, or recommendations before
deterministic backend validation and explicit confirmation or manual creation.

AI and OCR providers cannot write Income or Expense storage, publish financial
lifecycle events, change record status, or override backend validation.

### FST-003: Only active records participate

Current calculations include exactly the authenticated owner's active,
authoritative records within the requested period:

```text
active income total(currency, period) =
  sum(active Income records for owner, currency, and inclusive period)

active expense total(currency, period) =
  sum(active Expense records for owner, currency, and inclusive period)

net(currency, period) =
  active income total(currency, period)
  - active expense total(currency, period)
```

Draft, rejected, failed, incomplete, unknown-type, transfer-placeholder, and
archived records contribute zero. A projection may lag event delivery, but it
must expose or retain enough version/checkpoint data to avoid presenting a
partial result as final.

### FST-004: Ownership is part of every query

Every write, lookup, list, calculation, projection, correction, archive, and
restore is scoped by the authenticated owner ID established at the trusted
gateway boundary. A caller cannot select or override another owner ID.

A record belonging to another owner is treated as absent. Aggregations must
filter by owner before grouping or summing. Shared caches and read models must
include owner scope in keys and authorization checks.

### FST-005: Currencies remain separate in the MVP

Amounts use validated decimal values rounded to the owning service's supported
precision. Totals group by normalized ISO currency. The MVP must never add,
subtract, compare, or silently convert unlike currencies.

A multi-currency result is a set of per-currency totals. Conversion is allowed
only after a future, separately approved exchange-rate policy defines rate
source, timestamp, rounding, provenance, and failure behavior. Missing rates
must not trigger an implicit fallback or a one-to-one conversion.

### FST-006: Corrections replace current values; history remains auditable

An accepted correction updates the owner-scoped authoritative record through
the owning service, increments its revision, and causes current calculations to
use the new values exactly once. Corrections must not create a second active
copy of the same logical transaction.

The previous revision or equivalent audit evidence must remain recoverable in
durable production storage. Downstream projections process lifecycle events
idempotently and order or reject stale revisions.

### FST-007: Archive and restore are reversible state transitions

Archiving retains the record and audit history but removes it from current
calculations. Repeated archive requests are idempotent.

Restoring revalidates the stored financial fields under current deterministic
rules. A successful restore returns the record to active calculations exactly
once and increments its revision. A failed restore leaves the record archived.
Repeated restore requests are idempotent.

### FST-008: Derived systems are read-only consumers

Balance, reporting, limit, score, analytics, and recommendation components may
calculate and cache derived results. They cannot mutate authoritative financial
records or treat their own cache, model output, score, recommendation, or
notification as financial truth.

Rebuilds must produce the same result from the same authoritative record
versions, owner, period, and currency. When data is incomplete or stale, the
component must return an explicit partial/unavailable state rather than invent
values.

## Edge-Case Expectations

| Case | Required result |
| --- | --- |
| Draft is created or edited | No financial total changes |
| AI/OCR changes confidence or extracted values | No financial total changes |
| Same confirmation event is delivered twice | One authoritative record and one contribution |
| Manual and confirmed records have similar fields | Both count if they have distinct record identities; no heuristic deduplication |
| Record belongs to another user | Not visible and contributes zero |
| Record is archived inside a queried period | Retained for audit and contributes zero |
| Archived record is restored | Contributes once after successful revalidation |
| Active record amount/date/category is corrected | Old revision stops contributing; new revision contributes once |
| Correction changes currency | Removed from old currency total and added to new currency total |
| Income and expense use different currencies | Separate totals; no net value across currencies |
| Unknown or unsupported currency arrives | Validation fails; no authoritative record is created |
| Event arrives out of order | Stale revision is ignored or rejected; latest accepted revision wins |
| Projection is rebuilding or behind | Result is marked partial/unavailable until its completeness contract is met |
| Authoritative service is unavailable | No provider or cached suggestion may be promoted as a fallback |

## Test and Review Contract

Implementations and reviews should map coverage to the rule IDs above. At
minimum, automated tests should prove:

- drafts and suggestion sources never alter totals;
- confirmed and manual active records contribute exactly once;
- owner isolation applies to records and aggregates;
- archived records are excluded and valid restores re-enter exactly once;
- corrections replace, rather than duplicate, contributions;
- totals are deterministic and grouped by currency;
- duplicate, stale, and out-of-order lifecycle inputs are safe;
- incomplete projections cannot be presented as final.

A code review must reject any path that lets an AI/OCR adapter, client-provided
owner ID, cross-service database read, unsupported currency conversion, or
unconfirmed draft bypass these rules.

## Related Boundaries

- `docs/engineering/transaction-intake-draft-flow.md`
- `backend/services/income/README.md`
- `backend/services/expense/README.md`
- `docs/events/event-contract-versioning.md`
