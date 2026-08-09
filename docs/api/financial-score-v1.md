# Financial Score API v1

Related Jira: FIN-29, FIN-131, FIN-132.

The Financial Score Service owns current score and score history. The Public API Gateway is the intended client boundary; direct service routes are internal.

## Authentication

All routes require the trusted gateway authentication header and an authenticated gateway user ID header. The service hashes the user ID before reading state. Clients cannot request another user or provide a score.

## Current score

```http
GET /api/v1/financial-score/current?currency=USD
GET /financial-score/current?currency=USD
```

`currency` is a required three-letter code. The response includes `calculationId`, `currency`, integer `score`, `formulaVersion`, factors, and `calculatedAtUtc`. Each factor exposes a stable `code`, numeric `contribution`, safe `explanation`, and structured `inputs` containing `code`, numeric `value`, and `unit`.

Formula `financial-score-v2` uses confirmed records plus Profile-owned budget and onboarding settings. No client, LLM, or explanation provider can submit the final score.

The first authenticated current-score request for a user/currency without a stored calculation atomically persists and returns the neutral `financial-score-v2` score 50. This default snapshot is stable across repeated reads, appears in history, and does not publish a synthetic financial event. A later confirmed financial event replaces it as current while preserving history.

## Score history

```http
GET /api/v1/financial-score/history?currency=USD&fromUtc=2026-08-01T00:00:00Z&toUtc=2026-08-31T23:59:59Z&limit=20
GET /financial-score/history?currency=USD&fromUtc=2026-08-01T00:00:00Z&toUtc=2026-08-31T23:59:59Z&limit=20&beforeUtc=2026-08-20T12:00:00Z&beforeCalculationId=score-example
```

`limit` defaults to 20 and must be from 1 through 100. `fromUtc` and `toUtc` are an optional inclusive period pair; both must be supplied together and the start cannot be after the end. The optional cursor is the pair `beforeUtc` and `beforeCalculationId`; both values must also be supplied together. Period filtering is applied before the stable composite cursor, so pagination never leaks calculations outside the requested period. Results are newest first and return `items`, the effective `limit`, normalized `fromUtc` and `toUtc`, `hasMore`, `nextBeforeUtc`, and `nextBeforeCalculationId`.

The contract never exposes owner hashes, source financial record IDs, source event IDs, revisions, category/merchant text, receipt/OCR content, prompts, or provider output.
