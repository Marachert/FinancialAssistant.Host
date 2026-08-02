# Analytics Dashboard API v1

Related Jira: FIN-28, FIN-128.

The service-contract routes are:

```text
GET /api/v1/analytics/dashboard
GET /analytics/dashboard
```

The shorter route is the intended gateway alias after the public gateway route and
destination are explicitly activated. Both checked-in service routes require the
trusted gateway secret and authenticated user context headers.

Query parameters:

| Name | Required | Rule |
| --- | --- | --- |
| `currency` | yes | three-letter currency; currencies are never combined |
| `timeZoneId` | yes | valid platform time-zone identifier |
| `referenceDate` | no | local calendar date; defaults from the supplied time zone |
| `trendDays` | no | 1-31 inclusive; defaults to 7 |

The response includes currency/time-zone/reference date, explicit daily, weekly,
and monthly summary objects, daily limit status, monthly progress, monthly
category totals, zero-filled recent trend points, and freshness.

Each `dailySummary`, `weeklySummary`, and `monthlySummary` contains
`periodStart`, `periodEnd`, `incomeTotal`, `expenseTotal`, and
`balanceDelta`. Empty periods return zero totals. Daily boundaries use the
local `referenceDate`; weeks run Monday through Sunday; month boundaries use
the local calendar month. The API derives `referenceDate` from `timeZoneId`
when omitted, so UTC instants are never used as implicit local period boundaries. It excludes owner hashes, record and event IDs, revisions, origins,
storage details, and raw inputs. The daily limit is resolved server-side from the
authoritative limit provider and cannot be supplied by a caller. Missing,
unparseable, or invalid query values return `400` with
`invalid_analytics_request`; missing trusted context returns `401`.
