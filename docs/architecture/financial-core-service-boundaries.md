# Financial Core Service Boundaries

Status: Accepted for the POC
Owner: Financial core services
Jira: FIN-103

## Purpose

This document defines ownership and interaction boundaries for Profile, Category,
Transaction Intake, Income, Expense, and Financial Summary. It is normative for
implementation review. Service-local documentation may add detail but must not
move data ownership, create cross-service database access, or weaken the
[financial source-of-truth rules](financial-source-of-truth.md).

The Public API Gateway authenticates the caller and establishes the trusted owner
context. It is a technical perimeter, not an owner of financial business data.

## Boundary principles

### FCB-001: One owner for every mutable fact

Each mutable fact has exactly one owning service and one owning store. Other
services use a versioned REST contract, a versioned event, or a rebuildable local
projection. They must not read or write another service's database, tables,
indexes, object-storage namespace, cache, inbox, or outbox.

Shared contract packages define transport shapes only. They do not create shared
storage or shared authority.

### FCB-002: Authoritative financial writes stay separated

Only Income owns income records and only Expense owns expense records. Transaction
Intake owns user intent and drafts, but confirmation is not authoritative until
the matching owning service validates and commits its record. Profile, Category,
Financial Summary, AI, OCR, clients, and the gateway cannot create or mutate an
authoritative financial record.

### FCB-003: Synchronous reads are narrow and request-scoped

A synchronous cross-service request is allowed only when the caller needs current
owner-scoped reference data to complete the current user request. It must use a
versioned authenticated API, a bounded timeout, and explicit failure behavior.
It must not create a distributed transaction, synchronously chain domain writes,
or conceal an unavailable owner behind guessed or provider-generated values.

### FCB-004: State propagation is asynchronous

Business state that other services need after a committed change propagates by
versioned events and service-owned transactional outboxes. Consumers use
service-owned inboxes, process at-least-once delivery idempotently, reject stale
revisions, and keep replayable projections. A broker message is evidence of an
owned state change, not a replacement source of truth.

### FCB-005: Owner and privacy boundaries travel with every interaction

The gateway-derived owner is mandatory for public commands and queries.
Cross-service calls cannot accept a caller-selected owner override. Events use
the minimum identity representation required by their contract, including a
pseudonymous owner hash where raw identity is unnecessary. Logs, metrics, and
dead-letter metadata exclude payloads, raw input, receipts, OCR text, prompts,
credentials, and unnecessary financial details.

## Ownership matrix

| Service | Owned data and decisions | Does not own |
| --- | --- | --- |
| Profile Service | Owner-scoped locale, timezone, default currency, first day of week, privacy mode, AI-personalization consent, budget and notification preferences, onboarding flags, and Profile-owned persistence | Identity credentials and sessions; category taxonomy; drafts; income or expense records; balances; summary projections; provider output |
| Category Service | Stable income/expense taxonomy, presentation keys, owner-scoped aliases, deterministic search and match ordering, category validation contracts, Category-owned persistence, and category change events | Transactions, drafts, amounts, balances, reports, receipt/OCR data, LLM output, or authoritative financial calculations |
| Transaction Intake | Source references, normalized user intent, parser suggestions, ambiguities, draft revisions and review state, idempotency records, confirmation intent, Intake inbox/outbox state, and intake events | Authoritative income/expense records, record lifecycle, active totals, category taxonomy, user preferences, dashboard projections, or provider output as truth |
| Income Service | Owner-scoped authoritative income records, validation, origin, revisions, archive/restore lifecycle, active per-currency income totals, Income inbox/outbox state, and income lifecycle events | Expense records, drafts and parsing, category taxonomy, profile preferences, combined balances, dashboard summaries, OCR, or LLM output |
| Expense Service | Owner-scoped authoritative expense records, validation, origin, revisions, archive/restore lifecycle, active per-currency expense totals, Expense inbox/outbox state, and expense lifecycle events | Income records, drafts and parsing, category taxonomy, profile preferences, combined balances, dashboard summaries, OCR, or LLM output |
| Financial Summary | Disposable owner-hash and currency-scoped projections, daily/weekly/monthly totals, balance delta, category breakdown, event checkpoints, rebuild generations, and freshness/staleness metadata | Authoritative records, confirmation, correction, archive/restore commands, transaction drafts, category taxonomy, user preferences, or source events |

Identity Service remains outside this financial-core ownership set. It owns
accounts, authentication methods, credentials, provider links, and sessions, and
publishes `user.registered.v1`.

## Synchronous interaction rules

### Public and client-facing calls

Clients call each public API through the gateway. The gateway authenticates,
authorizes, strips untrusted internal headers, adds trusted owner context, and
routes the request. It does not join service data or execute financial rules.

The allowed client-facing ownership routes are:

| Capability | Owning synchronous API |
| --- | --- |
| Read/update preferences | Profile Service |
| Search categories/update aliases | Category Service |
| Create/review/confirm a draft | Transaction Intake |
| Create/list/read/update/archive/restore income | Income Service |
| Create/list/read/update/archive/restore expense | Expense Service |
| Read dashboard totals and freshness | Financial Summary |

### Cross-service synchronous reads

Transaction Intake may read Profile's versioned contract for default currency,
locale, and timezone when creating a draft. It may read Category's versioned
search/validation contract to produce category candidates. These calls are
owner-scoped and read-only.

If Profile is unavailable, Intake must not invent preferences. It may use an
already versioned, owner-scoped local projection only when its age and provenance
are explicit; otherwise it returns an unavailable result or a review-required
draft with the missing preference identified.

If Category is unavailable, Intake must not promote an AI/OCR category guess to
a valid category. It may retain the suggestion as an ambiguity and require review,
or fail the request with an explicit dependency-unavailable response.

Income and Expense validate all authoritative command fields locally. For future
user-created categories they may use a versioned Category validation API or a
Category-owned event projection. They must not query Category storage directly.

Financial Summary does not synchronously call Income or Expense to hide projection
lag. It returns the latest projection with explicit freshness metadata. Profile,
Category, Income, Expense, and Transaction Intake do not synchronously query
Financial Summary as part of a write.

### Forbidden synchronous coupling

The following patterns are prohibited:

- direct cross-service database, index, cache, inbox, outbox, or file access;
- one service opening a transaction over another service's store;
- synchronous Income and Expense writes inside an Intake database transaction;
- request-time joins across Income and Expense to build the dashboard;
- a client, gateway, AI adapter, or OCR adapter supplying trusted owner identity;
- using a stale cache or provider suggestion as silent financial truth;
- requiring a downstream service to be available merely to acknowledge an
  already committed local state change.

## Asynchronous interaction map

| Producer | Event | Authorized consumers and effect |
| --- | --- | --- |
| Identity | `user.registered.v1` | Profile creates default preferences; Category seeds the default owner catalog. Consumers deduplicate by event/business identity. |
| Category | `category.updated.v1` | Transaction Intake and future validators may refresh owner-scoped category projections. It never changes an existing financial record automatically. |
| Transaction Intake | `transaction.draft-created.v1` | AI/OCR orchestration may enrich the referenced draft through authenticated suggestion channels. The event contains no authoritative financial fact and changes no totals. |
| Transaction Intake | `transaction.confirmed.v1` | Exactly the matching Income or Expense consumer validates and commits one authoritative record idempotently. The nonmatching consumer ignores/rejects the type safely. |
| Income | `income.created.v1`, `income.updated.v1`, `income.archived.v1`, `income.restored.v1` | Financial Summary updates its Income-derived projection by record identity and revision. Analytics/limits may consume under separately documented contracts. |
| Expense | `expense.created.v1`, `expense.updated.v1`, `expense.archived.v1`, `expense.restored.v1` | Financial Summary updates its Expense-derived projection by record identity and revision. Analytics/limits may consume under separately documented contracts. |

Financial Summary publishes no command that mutates the six services. A later
summary-specific notification or cache-invalidation event must describe derived
state explicitly and cannot be consumed as an authoritative financial record.

## Command and event direction

```text
Client -> Gateway -> Profile API
                  -> Category API
                  -> Transaction Intake API
                  -> Income API
                  -> Expense API
                  -> Financial Summary API

Identity --user.registered.v1--> Profile
Identity --user.registered.v1--> Category

Profile --versioned read API--> Transaction Intake
Category --versioned read API / category.updated.v1--> Transaction Intake

Transaction Intake --transaction.confirmed.v1--> Income OR Expense
Income --income.*.v1--> Financial Summary
Expense --expense.*.v1--> Financial Summary
```

The arrows show contract direction, not storage access. Production confirmation
uses durable asynchronous delivery. An in-process development adapter may invoke
the same independently validating consumer synchronously for local tests, but it
must preserve the event contract, ownership, idempotency, and failure semantics
and must not become a shared persistence shortcut.

## Consistency, retries, and failure ownership

1. A service commits its own state and outbox atomically before publishing an
   owned event.
2. A consumer commits its inbox marker and local state/projection atomically
   where durable adapters support it.
3. Redelivery is expected. Event IDs deduplicate serialized messages; stable
   transaction/record identities and revisions deduplicate business effects.
4. Transient dependency and broker failures use bounded retries. Invalid
   contracts, unsupported versions, ownership failures, and privacy violations
   are terminal and use safe reason codes.
5. A failed downstream consumer never rolls back an already committed producer
   record. The producer reports its own result; delivery recovery uses the
   outbox, retry, and dead-letter process.
6. Financial Summary may lag and rebuild. It exposes stale/partial/unavailable
   state and never fabricates fresh totals.
7. Rebuilds consume authorized Income/Expense event history or snapshots through
   owned contracts. They never scan another service's tables.
8. Unlike currencies remain separate at every service and interaction boundary.

## Implementation review checklist

A change crossing a financial-core boundary is acceptable only when reviewers can
answer yes to all applicable questions:

- Is one service named as owner of every mutable field and business decision?
- Does each write commit only to the owning service's storage?
- Is every cross-service read versioned, authenticated, owner-scoped, bounded,
  read-only, and equipped with explicit failure behavior?
- Is state propagation asynchronous through an owned versioned event where the
  caller does not need an immediate reference answer?
- Are outbox, inbox, idempotency, replay, stale revision, and retry semantics
  defined for each event side effect?
- Can drafts, AI/OCR suggestions, the gateway, clients, and derived projections
  never bypass Income/Expense validation?
- Are raw identities and sensitive payloads excluded unless strictly required?
- Can each projection be rebuilt without direct access to another service's
  storage?
- Do tests cover owner isolation, duplicate delivery, dependency failure, and
  authority boundaries with synthetic data?

## Related contracts

- `docs/architecture/financial-source-of-truth.md`
- `docs/engineering/profile-service-preferences.md`
- `docs/engineering/category-service-taxonomy-and-aliases.md`
- `docs/engineering/transaction-intake-draft-flow.md`
- `docs/events/event-contract-versioning.md`
- `docs/engineering/financial-summary-read-model.md`
- `docs/api/financial-summary-v1.md`
- `backend/services/income/README.md`
- `backend/services/expense/README.md`
