# Financial Summary API Contract

Related Jira: FIN-101.

Owner: Financial Summary Service. Client entry point: Public API Gateway.

## Request

```http
GET /api/v1/financial-summary?currency=USD&timeZoneId=Europe%2FKyiv&referenceDate=2026-08-20
```

The gateway alias is `GET /financial-summary`. Authentication and trusted user
context are required. The owning service resolves that user to its pseudonymous
projection identity; clients never send or receive `userIdHash`.

Query fields:

| Field | Required | Meaning |
| --- | --- | --- |
| `currency` | yes | One explicit uppercase three-letter MVP currency; no conversion or cross-currency sum |
| `timeZoneId` | yes | IANA time-zone identifier used to resolve local calendar boundaries |
| `referenceDate` | no | ISO `YYYY-MM-DD`; omitted means the current local date in `timeZoneId` |

The response always echoes the normalized currency, validated time-zone ID, and
resolved reference date. Daily is that local date. Weekly is the containing
Monday through Sunday. Monthly is the containing calendar month. Every boundary
is inclusive.

## Response

```json
{
  "currency": "USD",
  "timeZoneId": "Europe/Kyiv",
  "referenceDate": "2026-08-20",
  "daily": {
    "period": "daily",
    "from": "2026-08-20",
    "to": "2026-08-20",
    "incomeTotal": 100.00,
    "expenseTotal": 40.00,
    "balanceDelta": 60.00
  },
  "weekly": {
    "period": "weekly",
    "from": "2026-08-17",
    "to": "2026-08-23",
    "incomeTotal": 200.00,
    "expenseTotal": 75.00,
    "balanceDelta": 125.00
  },
  "monthly": {
    "period": "monthly",
    "from": "2026-08-01",
    "to": "2026-08-31",
    "incomeTotal": 500.00,
    "expenseTotal": 225.00,
    "balanceDelta": 275.00
  },
  "balanceDelta": 275.00,
  "categoryBreakdown": [
    {
      "categoryId": "expense.groceries",
      "incomeTotal": 0.00,
      "expenseTotal": 80.00,
      "balanceDelta": -80.00
    }
  ],
  "freshness": {
    "isStale": false,
    "lastEventAtUtc": "2026-08-20T12:00:00Z"
  }
}
```

Top-level `balanceDelta` and `categoryBreakdown` use the monthly period. Category
identifiers are stable taxonomy keys, not localized labels. The initial mobile
contract must tolerate an empty category array and additive category fields.

## Empty and stale state

A valid period with no active records returns `200`, zero numeric totals, and
`categoryBreakdown: []`; absence is not `404`. When no event has been projected,
`freshness.isStale` is `true` and `lastEventAtUtc` is `null`.

A stale projection also returns `200` with last-known totals, `isStale: true`,
and its last projected event time. Clients can display freshness without losing
the dashboard. The service does not hide lag by querying Income or Expense
synchronously.

## Validation and compatibility

Invalid currency, time-zone ID, or date returns `400 invalid_summary_query`.
Missing/invalid authentication returns the gateway's stable `401` response.
Projection implementation fields such as owner hash, event ID, record ID, revision,
origin, storage generation, and rebuild state are never part of this API.

Field removal, rename, type change, period semantic change, currency conversion,
or timezone semantic change requires a versioned route/contract. Additive optional
fields remain compatible when mobile and web clients can ignore them.
