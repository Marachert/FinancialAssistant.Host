# Analytics Dashboard Read Model

Related Jira: FIN-28, FIN-128, FIN-129.

## Ownership and authority

Analytics owns a disposable dashboard projection. Income and Expense remain the
only authoritative financial record sources. Analytics never confirms or mutates
transactions and never reads another service's database. Drafts, raw user input,
receipts, OCR text, prompts, and AI suggestions are excluded.

## Event inputs and persisted projection

The projector consumes the shared-envelope lifecycle contracts:

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

The hosted RabbitMQ consumer declares the durable quorum queue
`fa.analytics.financial-events.v1`, binds each exact routing key, validates the
serialized envelope, and acknowledges only after the projection succeeds.
Terminal contract failures are rejected to the consumer-owned dead-letter route.

An accepted event is keyed by owner hash, record type, and record ID. Only a
higher record revision replaces the current projection. After each accepted
revision, the store persists materialized owner-and-currency-scoped daily totals,
Monday-based weekly totals, calendar-month totals, and monthly category totals.
Currency changes rebuild both the previous and new currency snapshots. Archived
records remain available for revision ordering but are excluded from aggregates.

The checked-in in-memory adapter is limited to the single-instance PoC. A durable
adapter must preserve the same keys, revision comparison, atomic snapshot update,
currency isolation, and rebuild behavior.

## Summary read models

The dashboard exposes three explicit period summaries. `dailySummary` covers
the local reference date, `weeklySummary` covers the Monday-through-Sunday week
containing that date, and `monthlySummary` covers its local calendar month.
Every summary contains inclusive period boundaries plus income, expense, and
balance-delta totals. Periods without active records return all three totals as
zero.

The API converts the current UTC instant through the requested `timeZoneId`
before choosing a default `referenceDate`. An explicit reference date is also
interpreted in that local calendar. Weekly and monthly boundaries are calculated
from the resulting local date, so daylight-saving transitions do not shift a
record into a neighboring summary period.

## Deterministic calculations

For every date, Monday-based week, month, category, and trend point:

```text
incomeTotal = sum(active income amounts)
expenseTotal = sum(active expense amounts)
balanceDelta = incomeTotal - expenseTotal
expenseToIncomePercent = expenseTotal / incomeTotal * 100
```

The ratio is absent when monthly income is zero. Percentages are rounded to two
decimal places using midpoint-away-from-zero. Unlike currencies are never mixed.
Recent trends contain one zero-safe point per calendar date, including days with
no records.

Daily expense limits are resolved server-side through
`IAnalyticsDailyLimitProvider`, whose production adapter must use an authoritative
settings or limits source. Callers cannot submit a limit. Analytics calculates
spent, non-negative remaining amount, and usage percentage. When the provider has
no limit, the response explicitly returns `isConfigured = false` and does not
invent a financial value.

## Category breakdown reports

The category-breakdown read model supports daily, Monday-based weekly, and
calendar-month periods using the same local reference-date rules as the dashboard.
Each category returns confirmed-record income, expense, balance delta, and
separate income/expense percentage shares. Shares use the selected period's total
for that record type and return zero when the denominator is zero.

All categories are sorted deterministically by combined amount and category ID.
Top-income and top-expense lists are ranked independently by amount and then
category ID. Missing or whitespace category IDs are normalized to the stable
`uncategorized` fallback before projection. Archived records remain available
for revision ordering but are excluded from every breakdown. The breakdown response
propagates the snapshot freshness state and last projected event time so clients can
distinguish confirmed empty periods from delayed or never-built projections.

## Freshness, replay, and failures

Duplicate and out-of-order events are no-ops when their revision is not newer.
The process-local development/test rebuild clears the disposable projection and
replays authorized events by occurrence time and event ID. A production rebuild
must instead use the scoped, checkpointed staging and atomic-swap process in
`docs/engineering/analytics-rebuild-backfill.md`; it must never clear unrelated
owners or periods. Empty and lagging projections remain readable with explicit
`isStale` and `lastEventAtUtc` metadata.

Unsupported event types, invalid versions, missing owner hashes, non-positive
amounts, invalid currencies/categories/dates/statuses, and negative revisions are
terminal contract failures. Diagnostics may include safe IDs, event type, revision,
and lag, but never raw identity or financial payloads.


## Limit progress and tracking streaks

The dashboard exposes daily, Monday-to-Sunday weekly, and calendar-month expense
progress. Limits come only from the server-side settings boundary; each period
returns its local calendar boundaries, configured state, limit, confirmed spend,
non-negative remaining amount, and usage percentage rounded to two decimals.
Missing limits stay explicitly unconfigured.

The tracking streak counts consecutive local dates ending on `referenceDate`
that contain at least one active confirmed income or expense record. A gap resets
the current streak to zero. The response includes a short deterministic positive
message suitable for the mobile dashboard. Time-zone conversion happens before
the reference date is chosen, so resets follow the requested local calendar and
daylight-saving changes never move a record between calendar periods.
