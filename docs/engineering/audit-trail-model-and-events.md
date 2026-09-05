# Audit Trail Model and Sensitive Operation Events

Related Jira: FIN-193. Service baseline: FIN-39.

## Purpose

The Audit Service records immutable, privacy-safe facts that let authorized
support and compliance reviewers reconstruct sensitive operations. It is not an
authoritative financial store and cannot change a profile, transaction, income,
expense, session, or administrative result.

The owning backend service decides whether an operation succeeded. It then emits
`audit.recorded.v1`; Audit Service validates the event against the catalog and
stores only bounded metadata. OCR and LLM output never supplies audit facts.

## Record model

| Field | Source | Meaning and rule |
| --- | --- | --- |
| `auditId` | Audit Service | Deterministic hash-derived identifier for the source event. |
| `sourceEventId` | Envelope | Idempotency identity for one serialized audit event. |
| `occurredAtUtc` | Envelope | Time the authoritative operation completed. |
| `recordedAtUtc` | Audit Service | Time Audit Service appended the record. |
| `producer` | Envelope | Allowlisted service that owns the operation. |
| `correlationId` | Envelope | Safe end-to-end trace identifier used for lookup. |
| `causationId` | Envelope | Safe identifier of the command or event that caused the operation. |
| `subjectIdHash` | Envelope | Optional pseudonymous hash of the user affected by the operation. |
| `actorType` | Payload | `anonymous`, `user`, `admin`, `service`, or `system`. Missing v1 values default to `service`. |
| `actorIdHash` | Payload | Required 64-character lowercase hexadecimal pseudonymous hash for `user` and `admin`; prohibited for other actor types. |
| `domain` | Payload | Closed classification: `security`, `business`, `ai`, `admin`, or `mcp`. |
| `action` | Payload | Cataloged operation code, or bounded `tool.*` for the MCP compatibility family. |
| `outcome` | Payload | `succeeded`, `failed`, `denied`, or `accepted`. |
| `resourceType` | Payload | Cataloged resource class, never a resource identifier or description. |
| `failureCategory` | Payload | Optional bounded safe reason code; never an exception message. |
| `retentionClass` | Payload | `standard`, `security`, or `regulatory`. |
| `expiresAtUtc` | Audit Service | Occurrence time plus the configured retention period. |

`actor` answers who initiated the operation. `subject` answers whose state was
affected. A user changing their own expense can have equal pseudonymous hashes;
an admin support action can have different actor and subject hashes. Service and
system activity has no actor hash.

## Canonical event catalog

Every row is enforced by `AuditEventCatalog` and `AuditPolicy`. A producer cannot
change the domain, resource, retention class, or actor types associated with an
action.

| Action | Producer | Domain | Resource | Retention | Allowed actors |
| --- | --- | --- | --- | --- | --- |
| `profile.updated` | Profile Service | business | profile | standard | user, admin |
| `profile.preferences.updated` | Profile Service | business | profile | standard | user, admin |
| `profile.consent.updated` | Profile Service | business | profile | security | user, admin |
| `income.created` | Income Service | business | income | regulatory | user, admin, system |
| `income.updated` | Income Service | business | income | regulatory | user, admin, system |
| `income.archived` | Income Service | business | income | regulatory | user, admin, system |
| `income.restored` | Income Service | business | income | regulatory | user, admin, system |
| `expense.created` | Expense Service | business | expense | regulatory | user, admin, system |
| `expense.updated` | Expense Service | business | expense | regulatory | user, admin, system |
| `expense.archived` | Expense Service | business | expense | regulatory | user, admin, system |
| `expense.restored` | Expense Service | business | expense | regulatory | user, admin, system |
| `draft.confirmed` | Transaction Intake Service | business | transaction-draft | regulatory | user |
| `authentication.succeeded` | Identity Service | security | authentication-attempt | security | user, service |
| `authentication.failed` | Identity Service | security | authentication-attempt | security | anonymous, user, service |
| `session.created` | Identity Service | security | session | security | user, admin, system, service |
| `session.refreshed` | Identity Service | security | session | security | user, admin, system, service |
| `session.revoked` | Identity Service | security | session | security | user, admin, system, service |
| `admin.audit.viewed` | Audit Service | admin | audit-trail | security | admin |
| `admin.monitoring.viewed` | Monitoring Service | admin | monitoring-dashboard | security | admin |
| `admin.action.executed` | Audit, Monitoring, or MCP Service | admin | admin-operation | security | admin |

MCP tool execution retains the existing dynamic action family `tool.{safe-name}`
with producer `mcp-service`, domain `mcp`, resource `mcp-tool`, standard
retention, and a service actor. No other uncataloged action is accepted.

## Producer flow

1. The owning service authenticates and authorizes the caller before changing
   authoritative state.
2. Deterministic backend rules decide the operation result.
3. The owner creates an audit event with a new source event ID, the originating
   correlation and causation IDs, the affected subject hash when available, and
   one cataloged payload.
4. The owner publishes through its transactional outbox where the operation also
   mutates durable state. Authentication failures that do not mutate state use a
   security event path with the same delivery guarantees.
5. Audit Service authenticates the producer, validates the envelope, actor hash,
   catalog tuple, outcome, failure category, and configured retention class.
6. Audit Service appends the immutable record. At-least-once redelivery of the
   same source event is idempotent; conflicting replacement is rejected.
7. Expired records are excluded from queries and the durable store lifecycle
   removes them according to policy.

The catalog defines required emissions. Each owning service remains responsible
for wiring its events at the authoritative state-change boundary and proving
that success and failure paths cannot silently skip their audit event.

## Example application flow

The following synthetic flow shows a receipt-derived expense becoming a
confirmed financial record and later being reviewed by support:

1. A signed-in user uploads a receipt through the mobile client. The gateway
   validates the session and forwards a safe correlation ID.
2. Receipt Processing and AI Orchestration may propose fields, but Transaction
   Intake stores only a draft. Probabilistic extraction is not authoritative.
3. The user corrects and confirms the draft. Transaction Intake validates every
   field and commits the confirmation. It emits `draft.confirmed` with a user
   actor hash and the affected subject hash, but no amount, merchant, receipt,
   OCR text, or prompt.
4. Expense Service consumes the confirmed financial event, applies deterministic
   rules, stores the expense, and emits `expense.created` plus its audit event.
5. Audit Service appends both audit records under the propagated correlation ID.
   Analytics updates asynchronously from the authoritative financial event; it
   does not alter the audit facts.
6. When the user reports a problem, an authorized administrator opens the audit
   view through the gateway. The query returns only pseudonymous metadata and
   uses `Cache-Control: no-store`.
7. The reviewer follows correlation and causation from confirmation to expense
   creation, compares occurrence and ingestion times, and sees producer,
   actor/subject relationship, outcome, and safe failure category. Viewing the
   trail is itself represented by `admin.audit.viewed` when producer wiring is
   enabled.

## Support and compliance review

Support can answer whether an operation was attempted, which service owned it,
whether it succeeded or was denied, and which downstream operation it caused.
Compliance review can verify actor/subject separation, retention class, event
ordering, immutable source IDs, and gaps against dead-letter and outbox
telemetry.

The current admin REST API intentionally queries one correlation ID only. It
does not provide broad user search, raw identity lookup, arbitrary filtering,
bulk export, or mutation. Those capabilities require separate authorization,
privacy, retention, and operational design before implementation.

## Privacy and security boundary

Audit payloads must never contain names, email addresses, phone numbers,
addresses, access or refresh tokens, credentials, account numbers, amounts,
currency values, merchants, category descriptions, financial notes, receipt
images, OCR text, prompts, model responses, exception messages, or stack traces.

Only synthetic values are used in tests and examples. Authentication secrets are
environment-provided. The audit read endpoint requires gateway trust plus the
admin role, returns `no-store`, and does not make Audit Service a source of truth.

## Cost and PoC posture

The catalog and validation are in-process and add no paid provider or model
dependency. The PoC uses the existing in-memory append-only store. Durable
Elasticsearch retention, broad compliance reporting, and export remain future
operational work and must be explicitly budgeted before activation.
