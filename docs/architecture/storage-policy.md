# Service-Owned Storage Policy

Related maintenance: FIN-269. This reconciles the repository with the
[current Confluence storage decision](https://marachert.atlassian.net/wiki/spaces/FA/pages/491621)
and the preferred stack in `AGENTS.md` and project instructions.

## Decision And Implementation

The [Windows-native POC decision](windows-native-poc.md) changes installation,
not financial authority. FIN-274/275 implement missing durable service-owned
stores; FIN-277 supplies native protected receipt/cache adapters. Existing
Compose infrastructure is not the new POC deployment mechanism.

FIN-274's original multi-owner scope is explicitly decomposed: FIN-274 owns the
PostgreSQL foundation; FIN-282 Identity, FIN-283 Profile/Category, FIN-284 Intake,
FIN-285 Receipt/OCR/AI metadata and FIN-286 persistence integration retain the
required domain behavior and verification. Each is a separate leaf under FIN-270;
no original requirement is removed or claimed complete by foundation delivery.

FIN-275's original financial/read-model/audit persistence scope is also explicitly
split by ownership: FIN-275 Income/Expense and matching-owner event confirmation,
FIN-287 separate Summary/Analytics, FIN-288 separate Score/Recommendations/
Notifications/preferences, and FIN-289 append-only Audit metadata/retention.
See the [inventory and acceptance boundaries](../engineering/native-financial-persistence-scope.md).
All original requirements remain required under FIN-270; FIN-286 depends on all
four. FIN-284 follows its blocked financial-owner prerequisite FIN-275 before
Intake confirmation implementation. Planning alone did not implement adapters.
The subsequent [native financial implementation](../engineering/financial-records-native-persistence.md)
adds separate Income/Expense owned records, immutable confirmation originals/
fingerprints and atomic minimized outboxes; the derived/audit siblings remain pending.
FIN-284 commits only its durable confirmation outbox; FIN-276 delivers to the
matching FIN-275 financial owner asynchronously. Owner APIs are read-only status/
reconciliation for Intake, not a second confirmation write or direct SQL path;
FIN-285 coordinates FIN-277's encrypted object storage. Reliable lifecycle delivery
remains FIN-276. The [scope ledger](../agent/POC_PROGRESS.md) records the five extra
leaf units separately from feature completion and installed-host acceptance.

PostgreSQL is the preferred target for durable authoritative financial records,
workflow state, idempotency, inbox/outbox and audit persistence. Each service owns
its schema, access, migrations, retention and recovery; another service must use
the owner's API or versioned events, never its tables.

The [FIN-274 foundation contract](../../backend/shared/persistence/README.md)
defines explicit role/schema separation, encrypted records and atomic transactions.
Its scoped implementation is not a claim of complete native domain repositories, installed
deployment or full restore qualification. Current production service wiring includes
in-memory development adapters. The local Compose assets still include
Elasticsearch and service-owned index templates. Inspect each service's
dependency injection and the selected environment before asserting durability.

FIN-282 selects native Identity adapters; FIN-283 selects separate native
[Profile/Category adapters](../engineering/profile-category-native-persistence.md).
Their native profiles require protected stable keys/owner credentials with no
in-memory fallback. Category state and outbox intents commit atomically, but live
RabbitMQ delivery/inbox qualification and native readiness remain FIN-276. Active
preferences/catalogs are retained until authorized deletion; minimal replay
markers and pending intents are retained until a qualified lifecycle horizon.

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
