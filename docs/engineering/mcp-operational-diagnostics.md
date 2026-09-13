# MCP Operational Diagnostics

Related Jira: FIN-196, parent FIN-36. This is a **design-only** contract, not
deployed tooling. The [catalog](../api/mcp-diagnostics-catalog.json) is an offline
review/test artifact, not SDK registration or configuration that enables access.

## Implemented Boundary

The [MCP v1 host](../api/mcp-server-v1.md) currently registers exactly six tools:
`system_health`, `ai_cost_summary`, `parsing_quality`, `prompt_eval_summary`,
`jira_issue_draft`, and `architecture_lookup`. Existing role rules are unchanged.
Monitoring supplies aggregate snapshots; the Audit adapter records tool outcomes,
not audit searches. The four actions below are **not registered**.

Current health has no response freshness timestamp; cost and parsing tools can
return zero counters when Monitoring is unavailable. These zeros do not prove
no activity. The new availability envelope below is a target, not a claim that
the existing six handlers already implement it.

## Authority And Routing

All four new actions require an authenticated internal **admin** principal and
an explicitly approved local/POC environment. No production enablement, user
impersonation or caller-selected tenant/environment is part of this definition.
An opaque operation or correlation identifier is a selector, never authorization.
Support-case approval must precede real-data lookup; synthetic fixtures cannot
certify that approval. The current shared-secret/header role model provides
trusted internal POC access, not per-person identity or tenant authorization.

MCP calls an allowlisted owning-service API with separately configured service
trust. It never holds Elasticsearch/PostgreSQL credentials, resolves caller URLs,
or reads another service's storage. Missing APIs mean `unavailable`, never a raw
storage fallback. Admin cannot override policy through arguments. Actions are
read-only in business effects; required security audit appends are the sole side
effect. No job retry/replay, alias/retention change, snapshot restore, event
publication, Jira submission or financial mutation is exposed.

## Action Catalog

| Proposed action | Input | Owner and result | Bounds |
| --- | --- | --- | --- |
| `operational_query` | Exact `catalogKey` | Owner executes fixed FIN-195 query; typed aggregate buckets or component health | At most 20 items; no raw hits |
| `operational_index_inspect` | Exact `catalogKey` | Owner returns configured/alias-resolved/schema-match/retention-policy-match flags and safe projection status | One result; no settings bodies |
| `failed_job_lookup` | Random operational UUID and `ai`, `ocr` or `notification` kind | Monitoring returns latest matching failed observation | One item; retained window at most 24 hours |
| `audit_lookup` | Exact bounded correlation identifier | Audit returns unexpired minimized classification/timestamp records | At most 20 items from last 24 hours |

Unknown fields, wrong types, unknown keys, malformed or nil UUIDs and correlation
strings outside the catalog pattern are rejected before downstream access.
UUIDs must use canonical hyphenated format, never user, receipt, financial-record
or authentication identifiers. No free-text query, DSL, SQL, index name, wildcard,
regex, URL, shell, routing key, sort, arbitrary time range, page size or cursor
is accepted. The catalog specifies exact inputs and output field allowlists.

### Queries And Inspection

The exact keys map to the
[FIN-195 owner catalog](../../infra/elasticsearch/operations/catalog.json):
`application-logs` to Public API Gateway, `event-summaries` to Audit, and
`ai-ocr-job-observations` / `service-health` to Monitoring. Query/inspection API
adapters do not yet exist; their owners must implement and authorize them first.

The owner fixes environment, alias, component allowlist, fields and query body.
Use the [bounded query contracts](operational-indices-and-dashboards.md): fixed
windows (logs 15 minutes, audit 1 hour, failed AI/OCR observations 1 hour, health
5 minutes), 2-second Elasticsearch timeout and no partial results. Reject timeout,
failed shard, unknown bucket, missing required source and expired audit evidence.
Return typed fields only, never dependency response dumps. `bucketKey` is the
fixed query bucket name, not source text; AI/OCR keys combine fixed kind/category.
Query-specific shapes use only applicable fields from the catalog union; health
must not invent counts. Failed revisions are not distinct job totals or rates.

Inspection compares the owner's resolved read alias and schema/retention policy
with its configured contract, returning booleans only. Actual index/node/host
names, mappings, samples, shard errors, credentials and snapshot paths remain
inside the owner. Retention-policy match does not prove physical erasure or
restore success; these remain FIN-205/runtime gates.

### Failed Jobs And Audit

The [Monitoring jobs API](../api/monitoring-admin-v1.md) retains at most 200 latest
observations for 24 hours in process-local memory. A future adapter may filter
this bounded response by exact UUID/kind and `failed`, never returning other
records. Validate source/kind ownership and the five existing closed error
categories; omit the input identifier from output. Restart, eviction, absent
producer wiring or later non-failed revisions can yield `no_data`; absence does
not prove an operation never failed. This is not a queue or durable history.

[Audit lookup](../api/audit-admin-v1.md) must filter at the owner by exact
correlation, occurrence in the last 24 hours and expiry strictly after now.
Order newest occurrence first, with an internal stable ID for ties; return at
most 20 and `truncated=true` when more exist. Omit that ID, actor/subject hashes,
causation, amounts, notes and raw payloads. The existing Audit route is not a
bounded MCP projection: owner-side limiting, response-byte cap and a minimized
adapter are prerequisites. Never download an unrestricted trace then truncate
in MCP. Broad identity search and support UI activation stay disabled pending
separately approved privacy scope.

## Response And Failure Policy

Each proposed response has `outcome`, server `generatedAtUtc`, nullable
`sourceObservedAtUtc`, `truncated`, fixed `classification` of
`bounded-operational-metadata-only`, and typed `items`. Cap encoded UTF-8 output
at 16 KiB and total call time at 5 seconds. Missing/invalid timestamps, unknown
fields/classifications or oversized results fail closed without payload.
`sourceObservedAtUtc` is the completed owner read/projection watermark, separate
from individual event/job times. A watermark older than 5 minutes or in the
future yields `stale`; absent watermark yields `unavailable` for live actions.

`ok` requires valid, fresh, complete evidence. `no_data` is a successful bounded
read with no matches, not a healthy system. `unavailable`, `stale`, `denied`,
`invalid_request` and `rate_limited` return empty items and no raw error details.
Planned audit truncation differs from a failed/partial backend search, which must
never return `ok`. Missing live probes cannot become synthetic healthy samples.

Enforce one concurrent call per authenticated principal, at least 30 seconds
between repeats of the same action/selector, and zero automatic retries. Apply
equivalent owner limits and propagate cancellation. Identity-bound rate limits
require an approved identity mechanism; a shared secret alone does not identify
distinct people. These are enablement prerequisites, not enforced by this file.

Authenticate, authorize, validate and rate-limit before fetching. Audit safe tool
name, server-generated request correlation, outcome, failure category and time,
never lookup arguments, returned items or raw exceptions. Keep lookup selectors
separate from audit correlation. Audit failure prevents releasing results. Audit
queries use an owner snapshot excluding their own audit append to avoid recursion.

## Example Support Flow

1. An approved admin checks existing `system_health`. Unavailable Monitoring or
   ambiguous zero counters are recorded as a visibility gap.
2. After future adapter approval, call `operational_index_inspect` with
   `{"catalogKey":"ai-ocr-job-observations"}`. If unavailable, escalate to the
   owner; do not access an index directly or enable infrastructure.
3. Call `operational_query` with the same key for bounded failure observations.
   No customer receipt, text or provider payload is needed for this category view.
4. With an approved synthetic case, call `failed_job_lookup` using kind `ocr`
   and UUID `11111111-2222-4333-8444-555555555555`. A `timeout` is operational
   evidence only, not proof that a financial transaction was created.
5. Independently authorized `audit_lookup` for `synthetic-case-001` can show
   minimized history. A random job UUID is not automatically an Audit correlation;
   their association needs owner-provided safe evidence.
6. Prepare a sanitized incident draft with category, freshness and next owner.
   A human/approved delivery workflow submits it. No retry, refund, mutation or
   raw export is offered. FIN-197 owns the wider support workflow.

## Acceptance Before Enablement

Offline tests verify exact roles/keys, closed fields, bounds, non-registration
and source links. They do not execute JSON Schema, Elasticsearch queries, SDK
handlers or downstream authorization. Before enabling adapters, test synthetic
owner APIs for wrong roles/environment, forged headers, unknown fields/keys,
SQL/DSL/URL inputs, invalid selectors, expiry, missing/future/stale watermarks,
partial/oversized results, timeout/cancellation, rate/concurrency limits and
audit failure. Assert zero owner calls on rejection, no raw output/log values,
no write routes and correct no-data/truncation. FIN-199/200/205 and P9 release
evidence must cover runtime behavior. Definition completion does not establish
first-user readiness or authorize extra spending.
