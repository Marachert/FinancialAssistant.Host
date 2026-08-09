# Financial Score API v1

Related Jira: FIN-29, FIN-131.

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

The endpoint returns `404 financial_score_not_found` until a confirmed financial event produces the first stored score. At the formula boundary, a user with no confirmed records receives the neutral default 50; FIN-132 owns the client-facing trigger and persistence behavior for that initial snapshot.

## Score history

```http
GET /api/v1/financial-score/history?currency=USD&limit=20&beforeUtc=2026-08-20T12:00:00Z&beforeCalculationId=score-example
GET /financial-score/history?currency=USD&limit=20&beforeUtc=2026-08-20T12:00:00Z&beforeCalculationId=score-example
```

`limit` defaults to 20 and must be from 1 through 100. The optional cursor is the pair `beforeUtc` and `beforeCalculationId`; both values must be supplied together. This composite cursor retains calculations that share a timestamp. Results are newest first and return `items`, the effective `limit`, `hasMore`, `nextBeforeUtc`, and `nextBeforeCalculationId`.

The contract never exposes owner hashes, source financial record IDs, source event IDs, revisions, category/merchant text, receipt/OCR content, prompts, or provider output.
