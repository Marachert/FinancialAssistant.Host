# Financial Score API v1

Related Jira: FIN-29.

The Financial Score Service owns current score and score history. The Public API Gateway is the intended client boundary; direct service routes are internal.

## Authentication

All routes require the trusted gateway authentication header and an authenticated gateway user ID header. The service hashes the user ID before reading state. Clients cannot request another user or provide a score.

## Current score

```http
GET /api/v1/financial-score/current?currency=USD
GET /financial-score/current?currency=USD
```

`currency` is a required three-letter code. The response includes `calculationId`, `currency`, integer `score`, `formulaVersion`, factor `code`, `contribution`, safe `explanation`, and `calculatedAtUtc`.

The endpoint returns `404 financial_score_not_found` until a confirmed financial event produces the first score.

## Score history

```http
GET /api/v1/financial-score/history?currency=USD&limit=20&beforeUtc=2026-08-20T12:00:00Z
GET /financial-score/history?currency=USD&limit=20&beforeUtc=2026-08-20T12:00:00Z
```

`limit` defaults to 20 and must be from 1 through 100. `beforeUtc` is an optional exclusive ISO 8601 cursor. Results are newest first and return `items`, the effective `limit`, and `hasMore`.

The contract never exposes owner hashes, source financial record IDs, source event IDs, revisions, category/merchant text, or semantic-provider content.
