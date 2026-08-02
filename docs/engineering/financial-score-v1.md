# Financial Score v1

Related Jira: FIN-29.

Financial Score Service derives a transparent personal score from confirmed Income and Expense lifecycle events. Income and Expense services remain authoritative for financial records; the score projection is disposable and rebuildable.

## Deterministic formula

Formula version `financial-score-v1` starts at 50 and clamps the final rounded result to 0 through 100.

| Factor | Range | Rule |
| --- | ---: | --- |
| Cash flow | -30 to +30 | 90-day `(income - expense) / income`, clamped to -1 through +1; expense without income is -30 |
| Monthly consistency | 0 to +10 | Share of observed months whose confirmed income is at least confirmed expense |
| Tracking coverage | 0 to +10 | Distinct confirmed-record days divided by 30, capped at 10 |
| Bounded semantic | -5 to +5 | Sum of optional reason-coded adjustments, each constrained to -2 through +2 |

Every result stores the formula version, exact factor contributions, safe explanations, currency, and calculation timestamp. Archived records and records outside the 90-day observation window do not contribute. Currencies are never mixed.

## Probabilistic boundary

The service does not invoke an LLM. A future authorized semantic provider may submit only reason-coded numeric adjustments to the application boundary. It cannot submit a final score, change deterministic financial inputs, exceed the per-factor bound, or exceed the total bound. Invalid semantic input fails closed.

## Event projection and history

The RabbitMQ consumer owns `fa.financial-score.financial-events.v1` and binds exactly:

```text
income.created.v1
income.updated.v1
income.archived.v1
income.restored.v1
expense.created.v1
expense.updated.v1
expense.archived.v1
expense.restored.v1
```

Record type, record ID, owner hash, currency, status, and revision form the idempotent projection boundary. Lower or equal non-identical revisions are ignored. A replay of the same event returns the previously stored calculation and republishes the same deterministic score event ID, allowing downstream inbox deduplication after a transient publish failure.

Each accepted new revision appends one history item and publishes `score.calculated.v1`. The payload contains no raw user identity, record ID, category, merchant, OCR text, prompt, or unrestricted model output.

## POC persistence

The default store and publisher are in-memory POC adapters. Production requires a durable projection/history store and outbox/inbox implementation that preserves the same revision, event identity, and formula-version rules. The default mode uses no paid services.
