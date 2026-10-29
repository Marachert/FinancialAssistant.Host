# Audit Service

Related Jira: FIN-39 and FIN-193.

Audit Service stores immutable, privacy-safe metadata for security, business,
AI, admin, and MCP operations. It is a technical trace service, not an
authoritative source for users, transactions, balances, receipts, prompts, or
other financial state.

## Event contract

Producers publish `IntegrationEventEnvelope<AuditEventV1>` with routing key
`audit.recorded.v1`. The payload contains only bounded identifiers: domain,
action, outcome, resource type, optional safe failure category, retention class,
actor type, and an optional pseudonymous actor hash. The sensitive-operation
catalog binds explicitly actor-annotated actions to their owner, domain,
resource, retention class, and allowed actors. Legacy v1 payloads without both
actor fields retain the previous bounded-identifier policy and use a service
actor compatibility default. See
[`docs/engineering/audit-trail-model-and-events.md`](../../../docs/engineering/audit-trail-model-and-events.md).

Raw email, phone, names, receipt/OCR text, prompts, model responses, financial
notes, amounts, descriptions, tokens, and credentials are prohibited. A
pseudonymous subject hash may be carried by the standard event envelope. User
and admin actors require a separate pseudonymous actor hash; anonymous, service,
and system actors cannot carry one.

## Append-only and delivery behavior

`IAuditRecordStore` exposes append and correlation lookup only. It has no update
or delete mutation. A repeated source event is idempotent when its immutable
content matches; a conflicting replay is rejected rather than replacing the
stored record. RabbitMQ uses an at-least-once quorum queue and dead-letters
invalid or failed deliveries without requeue.

Developer/test storage remains process-local memory. FIN-289's merged native
implementation uses separately owned PostgreSQL metadata, atomic correlation
indexes/replay receipts and bounded retention cleanup, without a memory fallback
or legacy consumer. Native PostgreSQL supersedes the historical Elasticsearch
write-alias target for this POC; Elasticsearch references remain compatibility
material, never financial authority. See [the native persistence contract](../../../docs/engineering/native-audit-persistence.md)
for explicit bounds, retention, verification and pending integration gates.
PR293 is independently verified merged as `f870733b35bc1d74a4248f882d4426faeef1e92e`
at `2026-10-29T13:53:43Z`; exact-head CI and re-review passed. This is locally
Verified Merged with Jira reconciliation pending, not installed/live delivery acceptance.

## Retention

Each allowlisted retention class maps to an explicit day count from 1 through
3650. `ExpiresAtUtc` is calculated from the authoritative event occurrence time.
Expired records are excluded from lookup. The PoC defaults are 365 days for
standard events, 730 days for security events, and 2555 days for regulatory
events. Production physical deletion belongs to the durable store lifecycle and
must not become an operator mutation endpoint.

## Access

- `POST /internal/audit/events` requires `Audit__Services__SharedSecret`.
- `GET /admin/audit?correlationId=...` requires trusted gateway authentication
  and the `admin` role.
- `Audit__Gateway__SharedSecret` must match the gateway downstream secret.
- RabbitMQ mode requires `Audit__Events__ConnectionString` from the environment.

All shared secrets require at least 32 characters and are never stored in
repository configuration.
