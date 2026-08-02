# Analytics Category Breakdown API v1

Related Jira: FIN-129.

The service-contract routes are:

```text
GET /api/v1/analytics/category-breakdown
GET /analytics/category-breakdown
```

Both routes require the trusted gateway secret and authenticated user context.

Query parameters:

| Name | Required | Rule |
| --- | --- | --- |
| `currency` | yes | three-letter currency; currencies are never combined |
| `timeZoneId` | yes | valid platform time-zone identifier |
| `period` | yes | `daily`, `weekly`, or `monthly` |
| `referenceDate` | no | local calendar date; defaults from the supplied time zone |
| `top` | no | 1-10 inclusive; defaults to 5 |

The response contains currency, time zone, local reference date, selected period,
inclusive period boundaries, all category rows, independently ranked top-income
and top-expense rows, and deterministic percentage shares. Every category row
contains `categoryId`, `incomeTotal`, `expenseTotal`, `balanceDelta`,
`incomeSharePercent`, and `expenseSharePercent`.

Daily periods cover the local reference date. Weekly periods run Monday through
Sunday, and monthly periods use the local calendar month. Empty periods return
empty category and top lists. A zero income or expense denominator produces a
zero share. Missing or whitespace category IDs use the stable `uncategorized`
fallback. Archived records are excluded.

The contract is designed for direct mobile dashboard consumption and never
exposes owner hashes, record/event IDs, revisions, raw inputs, storage details,
or cross-service database state. Invalid query values return `400` with
`invalid_analytics_request`; missing trusted context returns `401`.
