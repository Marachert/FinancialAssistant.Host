# Financial Assistant Analytics

.NET 8 deterministic analytics baseline for FIN-28.

Analytics owns a disposable, owner-hash and currency-scoped dashboard projection.
Income and Expense remain the authoritative financial sources. Drafts, raw input,
OCR data, and AI suggestions never enter analytics totals.

The projector consumes `income.created.v1` and `expense.created.v1` plus their
update, archive, and restore lifecycle events. Record revision makes duplicate and
out-of-order delivery idempotent. The store persists materialized daily,
Monday-based weekly, and calendar-month totals plus monthly category totals after
every accepted revision.
The in-memory adapter is for the single-instance PoC; a durable production adapter
must preserve the same owner/currency/date keys and revision rule.

`GET /api/v1/analytics/dashboard` and the intended gateway alias
`GET /analytics/dashboard` return daily expense-limit status, monthly income and
expense progress, monthly category totals, a zero-filled recent trend, and
freshness metadata. A caller may supply a positive `dailyExpenseLimit` obtained
from an authoritative settings/limits source. When omitted, the response says the
limit is unconfigured instead of inventing a financial value.

The API requires the trusted gateway secret and authenticated user headers. It
hashes the user identifier before reading the projection and never exposes owner
hashes, record/event identifiers, revisions, origins, or storage details.
