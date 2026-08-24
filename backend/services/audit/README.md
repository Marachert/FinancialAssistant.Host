# Audit Service

Related Jira: FIN-39.

Audit Service stores immutable, privacy-safe metadata for security, business,
AI, admin, and MCP operations. It is a technical trace service, not an
authoritative source for users, transactions, balances, receipts, prompts, or
other financial state.

## Event contract

Producers publish `IntegrationEventEnvelope<AuditEventV1>` with routing key
`audit.recorded.v1`. The payload contains only bounded identifiers: domain,
action, outcome, resource type, optional safe failure category, and retention
class.

Raw email, phone, names, receipt/OCR text, prompts, model responses, financial
notes, amounts, descriptions, tokens, and credentials are prohibited. A
pseudonymous subject hash may be carried by the standard event envelope.

## Append-only and delivery behavior

`IAuditRecordStore` exposes append and correlation lookup only. It has no update
or delete mutation. A repeated source event is idempotent when its immutable
content matches; a conflicting replay is rejected rather than replacing the
stored record. RabbitMQ uses an at-least-once quorum queue and dead-letters
invalid or failed deliveries without requeue.

The current PoC store is process-local memory. A durable implementation must
preserve the same append-only/idempotency contract and use the Audit-owned
Elasticsearch write alias defined by the platform naming policy.

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
