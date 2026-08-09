# Financial Score Service

The Financial Score Service calculates a deterministic, explainable personal financial score after confirmed income and expense lifecycle events.

## Ownership

- Formula version: `financial-score-v2`.
- Score range: 0 through 100; users without confirmed records start at neutral 50.
- Backend calculation is authoritative.
- Confirmed income and expense events plus explicit Profile settings are the only inputs.
- Factors cover monthly budget usage, 30-day spending trend, three-month income consistency, data completeness, and explicit penalty/cap policies.
- Non-empty semantic adjustments are rejected. An LLM or external provider cannot supply or override any contribution or final score.
- Every calculation stores factor contributions and safe factual explanation inputs and publishes `score.calculated.v1` with a deterministic event ID.

## POC storage

The checked-in adapters store projections, Profile-settings snapshots, and score history in memory for a single-process POC. `IFinancialScoreStore` and `IFinancialScoreProfileSettingsProvider` are the boundaries for durable production adapters. Profile settings must be synchronized from an authorized Profile API or minimal event, never by querying Profile storage directly. No paid OCR, LLM, database, or messaging service is invoked by the default development configuration.

## HTTP API

Trusted gateway callers use:

```text
GET /financial-score/current?currency=USD
GET /financial-score/history?currency=USD&fromUtc=2026-08-01T00:00:00Z&toUtc=2026-08-31T23:59:59Z&limit=20
```

The service also maps the canonical internal paths under `/api/v1/financial-score`. Both route sets require the configured gateway secret and user context headers. A first current-score read persists the neutral new-user snapshot without publishing a synthetic event. History accepts an inclusive `fromUtc`/`toUtc` pair and composes it with the existing stable cursor. Factor responses include safe structured explanation inputs.

## Runtime events

Set `FinancialScore:Events:Mode=RabbitMq` and provide `FinancialScore:Events:ConnectionString` to enable the durable quorum consumer and publisher. The default `InMemoryDevelopment` mode is isolated and free of external service cost.

RabbitMQ mode declares exact financial-event bindings plus durable 5-second, 30-second, and 5-minute retry queues on `fa.retry`. Currency moves recalculate both affected scopes, duplicate source events republish their deterministic stored result, and current-score reads follow accepted arrival order rather than cross-service source timestamps.
