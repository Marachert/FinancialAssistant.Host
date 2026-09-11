# Service-Owned Storage Policy

Related maintenance: FIN-269. This reconciles the repository with the
[current Confluence storage decision](https://marachert.atlassian.net/wiki/spaces/FA/pages/491621)
and the preferred stack in `AGENTS.md` and project instructions.

## Decision And Implementation

PostgreSQL is the preferred target for durable authoritative financial records,
workflow state, idempotency, inbox/outbox and audit persistence. Each service owns
its schema, access, migrations, retention and recovery; another service must use
the owner's API or versioned events, never its tables.

This is a target decision, not a claim that PostgreSQL repositories, migrations,
deployment or restore verification are complete. Current service wiring includes
in-memory development adapters. The local Compose assets still include
Elasticsearch and service-owned index templates. Inspect each service's
dependency injection and the selected environment before asserting durability.

Elasticsearch remains appropriate for controlled search, read projections,
operational indices and explicitly retained legacy adapter contracts. It is not
the universal authoritative financial database. Existing index naming, ownership,
privacy and compatibility rules remain applicable to those indices.

## Authority And Consistency

- Income and Expense own confirmed records; Summary, Analytics and Score own
  deterministic derived models. A projection is not a second transaction ledger.
- AI/OCR output is an editable candidate. It cannot become financial truth by
  being stored in a provider response, log, cache or search document.
- A durable state change and its outbox intent must have a documented atomicity
  boundary. Consumers deduplicate and update owned state consistently before
  acknowledging delivery. An in-memory outbox does not survive process failure.
- Redis is disposable cache/short-lived technical state. RabbitMQ transports
  events; neither replaces the authoritative store.
- Object storage owns protected binaries by service policy, not confirmed
  amounts or balances. Keep raw receipt/OCR/provider content out of broad events,
  diagnostics and documentation.

## Migration Gate

An implementation ticket must define the owning adapter, invariants, transaction
and concurrency behavior, idempotency, replay/backfill, privacy, retention,
backup/restore verification and rollback before replacing a store. Do not silently
rewrite existing API/event contracts, including decimal amounts, to fit a new
storage representation. Use synthetic migration tests; production or destructive
migration requires explicit authorization.

Earlier Elasticsearch-specific documents describe existing contracts or historical
designs. Their operational details are not approval to deploy them as the preferred
durable architecture. No data migration is performed by FIN-269.

## References

- [Financial source of truth](financial-source-of-truth.md)
- [Financial core boundaries](financial-core-service-boundaries.md)
- [Elasticsearch naming rules](../engineering/elasticsearch-index-naming.md)
- [Identity outbox limitations](../engineering/identity-event-publishing.md)
- [Current implementation](current-implementation.md)
