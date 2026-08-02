# Analytics Dashboard Read Model

Related Jira: FIN-28.

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

An accepted event is keyed by owner hash, record type, and record ID. Only a
higher record revision replaces the current projection. After each accepted
revision, the store persists materialized owner-and-currency-scoped daily totals,
calendar-month totals, and monthly category totals. Currency changes rebuild both
the previous and new currency snapshots. Archived records remain available for
revision ordering but are excluded from aggregates.

The checked-in in-memory adapter is limited to the single-instance PoC. A durable
adapter must preserve the same keys, revision comparison, atomic snapshot update,
currency isolation, and rebuild behavior.

## Deterministic calculations

For every date, month, category, and trend point:

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

Daily expense limits are supplied from an authoritative settings or limits source.
Analytics calculates spent, non-negative remaining amount, and usage percentage.
When no limit is supplied, the response explicitly returns `isConfigured = false`
and does not invent a financial value.

## Freshness, replay, and failures

Duplicate and out-of-order events are no-ops when their revision is not newer.
Rebuild clears the disposable projection and replays authorized events by
occurrence time and event ID. Empty and lagging projections remain readable with
explicit `isStale` and `lastEventAtUtc` metadata.

Unsupported event types, invalid versions, missing owner hashes, non-positive
amounts, invalid currencies/categories/dates/statuses, and negative revisions are
terminal contract failures. Diagnostics may include safe IDs, event type, revision,
and lag, but never raw identity or financial payloads.
