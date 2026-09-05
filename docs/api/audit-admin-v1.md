# Audit Admin API v1

Related Jira: FIN-39 and FIN-193.

## Purpose

The Audit API lets authorized support administrators trace important operations
by correlation identifier without exposing raw personal or financial content.
The response is append-only pseudonymous metadata, not a user or transaction
record.

## Query

```http
GET /admin/audit?correlationId=trace-safe-identifier
Authorization: Bearer <admin access token>
```

The Public API Gateway validates the token and admin role, removes caller-made
privileged headers, and injects its environment-backed downstream secret. Audit
Service independently validates that secret and the forwarded admin role.

The response contains deterministic audit/source-event identifiers, occurrence,
ingestion and expiry timestamps, producer/correlation/causation identifiers, an
optional pseudonymous subject hash, and allowlisted classification fields. It
also distinguishes the allowlisted actor type and optional pseudonymous actor
hash from the affected subject. Responses use `Cache-Control: no-store`.

## Internal ingestion

Trusted services submit the same versioned contract used by RabbitMQ:

```http
POST /internal/audit/events
X-Audit-Authentication: <environment secret>
Content-Type: application/json
```

The body is `IntegrationEventEnvelope<AuditEventV1>` with event type
`audit.recorded.v1` and schema version 1. Producer, domain, outcome, retention
class, action, resource type, actor type, actor hash, and failure category are
validated against the canonical sensitive-operation catalog and bounded
allowlists. Missing actor fields from an earlier `audit.recorded.v1` producer
default to a service actor. The internal secret is distinct from gateway trust.

## Privacy boundary

The contract has no arbitrary metadata dictionary or free-text field. Raw email,
phone, name, address, receipt/OCR content, prompts, model responses, financial
notes, amounts, descriptions, tokens, credentials, exception messages, and
stack traces are prohibited. Human actors use a 64-character lowercase
hexadecimal pseudonymous hash; raw actor and subject identifiers are prohibited.
Logs record only routing keys and exception types.

The catalog, producer flow, full application example, and review posture are
defined in
[`audit-trail-model-and-events.md`](../engineering/audit-trail-model-and-events.md).

## Retention and immutability

Audit records cannot be updated or individually deleted. Matching RabbitMQ
redelivery is idempotent; conflicting replacement is rejected. Expiry is
calculated from event occurrence time and an allowlisted retention class.
Expired records are not returned. Durable-store physical expiry must follow the
same policy without adding an operator mutation API.
