# Financial Assistant Financial Summary

.NET 8 event-derived financial summary baseline for FIN-100.

## Responsibility

Financial Summary owns a disposable read model for fast dashboard totals. Income
and Expense remain the authoritative write sources. This service never confirms,
corrects, archives, restores, or validates financial records and other services
must not treat its projection as authoritative financial state.

## Inputs

The projector consumes the shared-envelope v1 lifecycle events:

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

Drafts, parser suggestions, OCR output, and unconfirmed intake data never enter the
read model. Event identity and record revision make replay and out-of-order
delivery idempotent.

## Read model

A summary is owner-hash and currency scoped. It contains:

- daily, Monday-based weekly, and calendar-month income and expense totals;
- a monthly balance delta calculated as income minus expense;
- a monthly category breakdown grouped by stable category identifier;
- the reference date, period boundaries, last projected event time, and stale flag.

Archived records remain in the projection for replay and audit behavior but are
excluded from every total. Unlike currencies are never combined.

## Rebuild and freshness

The in-memory adapter rebuilds by clearing the disposable projection and replaying
the complete authorized event history in occurrence order. A durable adapter must
build a shadow generation, verify counts and totals, then atomically switch its
read alias so readers never observe a partial rebuild.

Queries always return their latest available projection. No-event summaries and
summaries older than the caller's configured freshness threshold return
`isStale = true` with zero or last-known totals and `lastEventAtUtc`. Clients may
show that state; the service must not synchronously query Income or Expense as a
hidden fallback.

## API contracts

FIN-101 adds the transport-only `FinancialAssistant.FinancialSummary.Contracts`
project and an explicit Application mapper. The stable gateway request is:

```text
GET /financial-summary?currency=USD&timeZoneId=Europe/Kyiv&referenceDate=YYYY-MM-DD
```

The response returns all three periods, monthly category/balance values, and
freshness metadata. Empty periods are zero-safe. Projection owner hashes, event
identifiers, record revisions, origins, storage generations, and rebuild state are
not exposed. A later endpoint implementation may depend on these contracts but
must not serialize the internal read model directly.
