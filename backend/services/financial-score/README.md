# Financial Score Service

The Financial Score Service calculates a deterministic, explainable personal financial score after confirmed income and expense lifecycle events.

## Ownership

- Formula version: `financial-score-v1`.
- Score range: 0 through 100.
- Backend calculation is authoritative.
- Confirmed income and expense events are the only financial inputs.
- Optional semantic factors are numeric adjustments bounded to `[-2, 2]` each and `[-5, 5]` in total. They cannot supply or override the final score.
- Every calculation stores factor contributions and publishes `score.calculated.v1` with a deterministic event ID.

## POC storage

The checked-in adapter stores projections and score history in memory for a single-process POC. `IFinancialScoreStore` is the persistence boundary for a durable production adapter. No paid OCR, LLM, database, or messaging service is invoked by the default development configuration.

## HTTP API

Trusted gateway callers use:

```text
GET /financial-score/current?currency=USD
GET /financial-score/history?currency=USD&limit=20&beforeUtc=2026-08-20T12:00:00Z&beforeCalculationId=score-example
```

The service also maps the canonical internal paths under `/api/v1/financial-score`. Both route sets require the configured gateway secret and user context headers.

## Runtime events

Set `FinancialScore:Events:Mode=RabbitMq` and provide `FinancialScore:Events:ConnectionString` to enable the durable quorum consumer and publisher. The default `InMemoryDevelopment` mode is isolated and free of external service cost.

RabbitMQ mode declares exact financial-event bindings plus durable 5-second, 30-second, and 5-minute retry queues on `fa.retry`. Currency moves recalculate both affected scopes, duplicate source events republish their deterministic stored result, and current-score reads follow accepted arrival order rather than cross-service source timestamps.
