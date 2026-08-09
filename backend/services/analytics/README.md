# Financial Assistant Analytics

.NET 8 deterministic analytics baseline for FIN-28.

Analytics owns a disposable, owner-hash and currency-scoped dashboard projection.
Income and Expense remain the authoritative financial sources. Drafts, raw input,
OCR data, and AI suggestions never enter analytics totals.

The hosted RabbitMQ consumer owns `fa.analytics.financial-events.v1` and binds
`income.created.v1` and `expense.created.v1` plus their update, archive, and
restore lifecycle events. It validates each serialized shared envelope and
acknowledges only after the projector succeeds. Record revision makes duplicate
and out-of-order delivery idempotent. The store persists materialized daily,
Monday-based weekly, and calendar-month totals plus monthly category totals after
every accepted revision.
The in-memory adapter is for the single-instance PoC; a durable production adapter
must preserve the same owner/currency/date keys and revision rule.

`GET /api/v1/analytics/dashboard` and the intended gateway alias
`GET /analytics/dashboard` return daily expense-limit status, monthly income and
expense progress, monthly category totals, a zero-filled recent trend, and
freshness metadata. The API resolves daily expense limits from the server-side
`IAnalyticsDailyLimitProvider`; callers cannot submit a limit. The PoC provider
returns unconfigured until populated by an authoritative settings/limits adapter,
instead of inventing a financial value.

The API requires the trusted gateway secret and authenticated user headers. It
hashes the user identifier before reading the projection and never exposes owner
hashes, record/event identifiers, revisions, origins, or storage details.


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

## Rebuild and backfill contract

`AnalyticsRebuildPlanner` validates an inclusive pseudonymous owner/period
scope and authoritative source snapshot version, then returns a stable job key
and ordered stages for analytics, score history, limit progress,
recommendation inputs, and verified atomic replacement. Progress and failure
contracts expose safe operational evidence without owner scope or financial
payloads.

No rebuild endpoint or executor is active. The process-local
`AnalyticsProjector.RebuildAsync` global reset remains a development/test
helper. Production work requires the trusted admin, durable checkpoint,
staging, high-water-mark replay, and owner/period atomic-swap controls defined
in `docs/engineering/analytics-rebuild-backfill.md`.
