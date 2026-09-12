# Operational Indices And Dashboard Queries

Related Jira: FIN-195. Status: design and offline query contracts, not deployed
Elasticsearch indices, collectors, lifecycle jobs or dashboard integrations.
The existing local stack pins Elasticsearch 8.15.3. No package, running cluster,
paid exporter or new infrastructure is required to validate these files.

## Ownership And Authority

Follow [index naming](elasticsearch-index-naming.md),
[storage policy](../architecture/storage-policy.md) and
[observability](../architecture/backend-observability-strategy.md).
PostgreSQL remains the preferred durable authoritative target. These Elasticsearch
families are minimized diagnostic projections, never financial truth or a
replacement for the authoritative append-only Audit store.

| Family | Owner and source | Physical example / stable aliases |
| --- | --- | --- |
| Application logs | Each emitting service; approved structured events, not raw console ingestion | `fa-local-public-api-gateway-application-logs-v1-000001`; `fa-local-public-api-gateway-application-logs-read` / `-write` |
| Audit event summaries | Audit Service; validated accepted audit events, stripped of actor/subject hashes and correlation IDs | `fa-local-audit-event-summaries-v1-000001`; `fa-local-audit-event-summaries-read` / `-write` |
| AI/OCR job observations | Monitoring Service; accepted, validated AI/OCR revisions from its signal boundary | `fa-local-monitoring-ai-ocr-job-observations-v1-000001`; `fa-local-monitoring-ai-ocr-job-observations-read` / `-write` |
| Service health | Monitoring Service; configured probes and bounded health results | `fa-local-monitoring-service-health-v1-000001`; `fa-local-monitoring-service-health-read` / `-write` |

Each alias suffix replaces the final `read`, not the whole physical index name.
The application-log example is one gateway-owned namespace; other services use
their own service segment. Monitoring must not read their log indices or ingest
raw logs. Gateway, mobile, admin web and MCP never receive Elasticsearch credentials.
An approved owner-side reader executes fixed queries; other services use the
owner's API. These files do not add such a reader or a public search endpoint.

An optional collector needs separate namespace-restricted identities for each
owner. Writers use only the owned write alias; readers use the owned read alias.
Template/alias/lifecycle administrators are separate from ingest/query identities.
No runtime `fa-local-*`, cross-service wildcard, user-supplied DSL or shared
business repository is authorized. Operator access must itself be audited.

## Projection Contract

All four families use `dynamic: strict`, UTC date fields and keyword enums, never
free-form `text`, dynamic objects or arbitrary metadata. The
[query catalog](../../infra/elasticsearch/operations/catalog.json) lists the exact
field/type allowlist. It is a design manifest, not an Elasticsearch template body.
An implementation must reject unknown fields and invalid values before storage;
strict mappings or `ignore_above` alone do not validate privacy or enum values.

- `observedAtUtc` is the server acceptance/probe time. Log and audit occurrence
  times retain the owner's original UTC time. Reject impossible future times;
  expired or excessive-lag input must not extend retention through replay.
- Log projection allows only controlled environment/service, numeric event ID,
  level, bounded outcome, status code and non-negative elapsed milliseconds.
  Map from approved structured event fields; drop console `Message`, `State`,
  `Scopes` and exception objects rather than forwarding serialized logs.
- Audit summaries retain producer, catalog action/domain/outcome and occurrence
  time, not actor/subject hashes, resource IDs or the original event payload.
  `expiresAtUtc` is the earlier of source expiry and the approved projection age;
  query-time filtering excludes expired summaries independently of physical ILM.
  Projection IDs deduplicate the accepted source event. Conflicting replacement
  is rejected. Full audit retention and evidence remain owned by Audit Service.
- Job observations retain only source service, `ai`/`ocr` kind, revision, state
  and closed error category. Enforce the existing source/kind and failure-category
  relationships in [Monitoring v1](../api/monitoring-admin-v1.md). One accepted
  revision is one observation; deduplicate its opaque operation/revision identity
  in the owner-side writer without exposing it as a dashboard field.
- Health records retain configured component, status, probe latency and observed
  time. Status is `healthy`, `degraded`, `unavailable`, `not_configured` or `unknown`.
  Deduplicate each component/observation-time pair and reject conflicting values
  so latest-time selection is unambiguous.
  No destination URL, host address, credentials or raw health response is stored.

No family accepts user/account/session/receipt/transaction IDs, amounts, currency,
merchant/category text, receipts, OCR text, prompts, completions, exception text,
connection strings, arbitrary labels or provider payloads. Do not repurpose
restricted AI/OCR source metadata as general Monitoring data. Financial corrections
must use deterministic owning APIs, never edits to an index.

## Draft Lifecycle And Retention

These are reviewable controlled-POC defaults, not legal retention advice or an
active deletion policy. Security/privacy and the release owner must approve the
actual deployment, costs, retention and disposal evidence before enabling export.

| Family | Proposed maximum query age | Lifecycle and recovery |
| --- | --- | --- |
| Application logs | 7 days from occurrence | Daily rollover or earlier at an approved size cap; no long-lived archive by default |
| Audit summaries | 7 days from occurrence, and never beyond source expiry | Rebuild only from unexpired authorized Audit records; does not shorten or extend authoritative audit retention |
| AI/OCR observations | 24 hours from server acceptance | Matches the current short diagnostic horizon; no replay of old source metadata to extend retention |
| Service health | 48 hours from probe time | New probes recover current state; historical gaps remain missing |

The current FIN-194 in-memory job store still caps 200 entries and expires them
24 hours after the last accepted update. This design does not change it or claim
durable producers. Projection samples and cumulative revision counts must not be
presented as complete job history or unique job totals.

For future alias rollover, bootstrap one write generation and mark exactly one
write target. The read alias includes all compatible unexpired generations;
otherwise rollover would hide history. Schema migration must separately specify
write continuity, replay/deduplication, atomic alias cutover and rollback under
the naming contract. Never combine incompatible schemas behind a dashboard alias.

ILM phase age after rollover is based on rollover time, not each document's event
time. Therefore a `delete.min_age` alone cannot enforce the maximum ages above.
An approved implementation needs query-time expiry filters and a bounded physical
deletion/snapshot-expiry design, including delayed ILM, late events and restore
behavior. Do not claim physical erasure from a query filter. Audit retention
classes and any authorized hold are resolved by the Audit owner, not generic ILM.
See [Elastic's lifecycle behavior](https://www.elastic.co/guide/en/elasticsearch/reference/8.15/ilm-index-lifecycle.html).

Snapshot/restore must not resurrect expired projections, bypass owner access or
copy production data into tests. Prefer regeneration where allowed. Collector
failure must not block financial writes; bound buffers, reject unsafe records and
report dropped/lagged signals without logging their payloads.

## Dashboard Queries

The [catalog](../../infra/elasticsearch/operations/catalog.json) binds four native
Search API JSON bodies to explicit local read aliases. Requests use
`POST /{readAlias}/_search?allow_partial_search_results=false`; no script is
provided to execute them automatically. In an approved owner-side reader, enforce
the alias server-side and reject missing indices, failed shards, `timed_out`,
transport failures and partial results. They are not zero activity or healthy state.
See [Search API](https://www.elastic.co/guide/en/elasticsearch/reference/8.15/search-search.html).

Each request has `size: 0`, `_source: false`, a two-second timeout and a bounded
time range. The only per-document result is a health sub-aggregation with a
one-record allowlist; its target component is explicitly selected. Queries are
read-only even though the HTTP method is POST. No arbitrary query strings, raw
document browsing, exports, tenant lookup or provider payload inspection.

| Query file | View and interpretation | Troubleshooting action |
| --- | --- | --- |
| `application-logs.json` | Last 15 minutes by controlled severity, with latest observation time | Inspect owner-approved events when warnings/errors rise; these are log-event counts, not request error rate |
| `audit-events.json` | Last hour by fixed outcome and latest recorded observation | A failed/denied bucket is not evidence of a financial write; use restricted Audit API for authorized correlation follow-up |
| `ai-ocr-jobs.json` | Last hour of failed AI/OCR observation revisions, by kind and safe failure category | Check provider availability, policy or result-validation gates through the owner; never fetch raw OCR/prompt input |
| `service-health.json` | Latest sample for explicitly selected `identity-service` in the last five minutes | Compare observed time with the probe budget; absent/stale/not-configured is never green, and optional dependency failure is not universal outage |

Filters use closed buckets instead of approximate top-N terms counts. Job buckets
count failure observations, not distinct failed jobs, failure percentages or a
current queue size. Health uses the latest observation, not the historical worst
status. Expand the component only from the configured allowlist, one bounded query
per component; expected inventory must expose components with no samples.
The [top-hits contract](https://www.elastic.co/guide/en/elasticsearch/reference/8.15/search-aggregations-metrics-top-hits-aggregation.html)
supports explicit sorting and source selection for this one-record sub-aggregation.

The future dashboard must show observation time, selected window, owner and data
availability. Refresh no faster than every 30 seconds, pause hidden views, cancel
obsolete requests and cap each user/session's concurrent queries. An empty
historical window is `No data`, not a successful probe or proof of no incidents.
No new panel is wired into the FIN-194 admin UI by this design ticket.

## Verification And Remaining Gates

Repository tests parse every manifest/query, check explicit owned aliases, field
allowlists, fixed bounds, no raw source projection, failure-only job filtering and
latest-sample health semantics. Tests are offline contract checks, not execution
against Elasticsearch. Existing runtime templates and bootstrap are unchanged.

Before runtime acceptance, FIN-205 and owning integration work must validate real
mappings/query results, duplicate/reordered signals, collector loss, missing and
stale data, multiple rollover generations, alias permissions, expiry, restore and
physical deletion using synthetic data on an approved host. FIN-196 owns MCP
diagnostics; FIN-198 alerts; FIN-199 tests; FIN-200 operational readiness. None is
marked delivered by this document. Keep the Confluence Index Catalog and
Operational Metrics pages aligned with this boundary.
