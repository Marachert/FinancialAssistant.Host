# Identity Data Model and Owned Storage

## Purpose

This document defines the FIN-85 storage contract for the Financial Assistant Identity Service.

Identity Service is authoritative for accounts, authentication credential metadata, refresh sessions, and external provider links. Elasticsearch is the operational store, but deterministic authentication and lifecycle rules remain in Application and Domain code.

FIN-85 defines documents, index names, aliases, sensitive-field rules, and cleanup policy. Elasticsearch client/bootstrap implementation is intentionally deferred to a later storage integration increment.

## Ownership boundary

Identity Service exclusively owns the `fa-{environment}-identity-*` namespace and the credentials that can read or write it.

Other services must use:

- synchronous Identity Service REST APIs for current identity/session decisions;
- versioned RabbitMQ events for lifecycle reactions;
- their own read models when identity facts are needed asynchronously.

Direct cross-service Elasticsearch reads, writes, joins, aliases, or shared documents are forbidden. A shared Elasticsearch cluster is infrastructure, not a shared database contract.

The API Gateway validates and forwards safe claims but never reads Identity Service indices.

## Index and alias convention

Physical indices follow:

```text
fa-{environment}-identity-{entity}-v{schemaVersion}-{generation}
```

The initial FIN-85 catalog is:

| Entity | Initial physical index in dev | Read alias | Write alias |
| --- | --- | --- | --- |
| accounts | `fa-dev-identity-accounts-v1-000001` | `fa-dev-identity-accounts-read` | `fa-dev-identity-accounts-write` |
| credentials | `fa-dev-identity-credentials-v1-000001` | `fa-dev-identity-credentials-read` | `fa-dev-identity-credentials-write` |
| sessions | `fa-dev-identity-sessions-v1-000001` | `fa-dev-identity-sessions-read` | `fa-dev-identity-sessions-write` |
| external identities | `fa-dev-identity-external-identities-v1-000001` | `fa-dev-identity-external-identities-read` | `fa-dev-identity-external-identities-write` |

Application repository adapters always use aliases. Physical names are used only by index bootstrap, rollover, reindex, migration, and operational tooling.

Environment segments are restricted to lowercase letters, numbers, and single hyphen separators. Schema versions and generations are positive integers. Generation is six-digit zero padded.

## Document model

Storage documents are Infrastructure models. They are not public API contracts and are not Domain entities.

All documents use an application-generated opaque identifier as Elasticsearch `_id`. Mutable updates must use Elasticsearch sequence number and primary term for optimistic concurrency when the active repository is implemented.

### Account document

Index: `accounts`

Fields:

| Field | Mapping intent | Notes |
| --- | --- | --- |
| `id` | keyword | Opaque account identifier; also document `_id` |
| `status` | keyword | Active, locked, disabled, deletion-pending, deleted |
| `roles` | keyword | Trusted role/scope assignment; no profile data |
| `createdAtUtc` | date | Creation timestamp |
| `updatedAtUtc` | date | Last authoritative update |
| `deletedAtUtc` | date, nullable | Deletion lifecycle timestamp |
| `schemaVersion` | integer | Document schema version |

The account document does not contain email, phone, password metadata, provider identifiers, financial profile data, or client preferences.

### Credential metadata document

Index: `credentials`

Fields:

| Field | Mapping intent | Notes |
| --- | --- | --- |
| `id` | keyword | Credential identifier and document `_id` |
| `accountId` | keyword | Owning account |
| `credentialKind` | keyword | Email-password, phone-password, or future local method |
| `lookupKeyHash` | keyword | Deterministic keyed hash of normalized email/phone used for exact lookup |
| `secretHash` | keyword with indexing disabled | Password/credential hash; never searched or logged |
| `secretHashAlgorithm` | keyword | Algorithm identifier, for example Argon2id or approved .NET hasher version |
| `secretHashParameters` | object/flattened with indexing disabled | Non-secret parameters required to verify and rehash |
| `isVerified` | boolean | Verification state |
| timestamps | date | Created, updated, verified, last rotated |
| `schemaVersion` | integer | Document schema version |

Raw email, raw phone, plaintext password, reset token, and verification code are forbidden.

`lookupKeyHash` must use a purpose-specific server-side HMAC/pepper so a leaked index cannot be used as a simple dictionary of normalized email addresses. The HMAC key is stored in a secret manager and is not an Elasticsearch field.

Only the current password/secret hash is retained. Rotation replaces the previous hash in the same authoritative update.

### Session document

Index: `sessions`

Fields:

| Field | Mapping intent | Notes |
| --- | --- | --- |
| `id` | keyword | Session identifier and document `_id`; carried separately from refresh secret |
| `accountId` | keyword | Owning account |
| `tokenFamilyIdHash` | keyword | Refresh-token family/reuse detection key |
| `refreshTokenHash` | keyword | Hash used to verify the presented refresh secret |
| `status` | keyword | Active, rotated, revoked, expired |
| `securityContextHash` | keyword, optional | Privacy-safe device/security context fingerprint |
| lifecycle timestamps | date | Issued, expires, rotated, revoked |
| `replacedBySessionId` | keyword, optional | Rotation chain pointer |
| `schemaVersion` | integer | Document schema version |

Access tokens are not stored. Refresh-token plaintext is returned once to the client and never persisted or logged. Stored refresh hashes must use a purpose-specific cryptographic hash/HMAC appropriate to token entropy and replay lookup.

### Provider link document

Index: `external-identities`

Fields:

| Field | Mapping intent | Notes |
| --- | --- | --- |
| `id` | keyword | Provider-link identifier and document `_id` |
| `accountId` | keyword | Owning account |
| `provider` | keyword | Google, Apple, or approved provider code |
| `providerSubjectHash` | keyword | Purpose-specific keyed hash of provider subject |
| `providerTenantHash` | keyword, optional | Keyed hash of tenant/issuer partition when required |
| lifecycle timestamps | date | Linked, last authenticated, unlinked |
| `schemaVersion` | integer | Document schema version |

Provider access tokens, refresh tokens, authorization codes, private keys, raw provider subject values, and provider email mirrors are forbidden.

## Mapping rules

Each index template must use `dynamic: strict` once the Elasticsearch bootstrap is implemented.

Recommended defaults:

- identifiers, states, roles, algorithms, and hashes: `keyword`;
- timestamps: `date`;
- schema version: `integer`;
- secret hashes and hash parameters that are not query keys: indexing and doc values disabled;
- no analyzed text fields;
- no wildcard or script-based authentication queries;
- no `_source` logging or document samples in operational diagnostics.

Identity repository queries are exact identifier/hash lookups only. Search relevance, full-text search, vector search, LLM, and OCR have no role in authentication decisions.

## Write consistency

Repositories will use read/write aliases and optimistic concurrency (`seq_no` and `primary_term`) for mutable documents.

Authoritative state changes follow this order:

1. validate deterministic business rules;
2. commit the Identity Service Elasticsearch write;
3. publish the versioned integration event using an outbox/reliable publication mechanism when FIN-77 is implemented.

RabbitMQ events do not replace the Identity Service record as the source of truth.

## Retention and cleanup

Domain cleanup decisions are executed by Identity Service workers. ILM manages rollover, old physical generations, snapshots, and cost; it must not delete active account or session documents merely because an index generation is old.

| Entity | Terminal trigger | Retention after terminal state | Hard maximum | Cleanup rule |
| --- | --- | --- | --- | --- |
| accounts | Account deletion accepted and holds cleared | 30 days | none for active accounts | Remove deleted tombstone after recovery window |
| credentials | Credential removed/replaced or account deleted | 30 days | none for active credential | Rotation removes superseded secret hash immediately; delete terminal document after window |
| sessions | Expired or revoked | 30 days | 90 days | Preserve briefly for replay detection, then hard-delete |
| external identities | Link removed or account deleted | 30 days | none for active link | Delete after unlink recovery window; audit history stays in Audit Service events |

Cleanup operations must be idempotent, use service-owned credentials, emit safe metrics, and never log document contents or secret/hash values.

Encrypted snapshots follow the platform backup policy. Restore drills must verify aliases and mappings before traffic is enabled.

## Security and access controls

- Use a dedicated Elasticsearch principal/role restricted to `fa-{environment}-identity-*`.
- Gateway, Profile Service, Monitoring Service, MCP Server, and admin clients receive no document-level access.
- Monitoring exposes only index health, shard state, size, ILM status, and error counts.
- MCP tools never expose arbitrary Elasticsearch queries or identity documents.
- TLS is required outside local development; encrypted disks/snapshots are required for hosted environments.
- HMAC/pepper keys, JWT signing keys, OAuth secrets, and Elasticsearch credentials live in a secret manager.
- Logs and traces contain correlation/trace identifiers, operation names, result classes, and duration only.

## Tests and verification

FIN-85 automated tests verify:

- four owned index definitions and stable aliases;
- naming convention validation;
- one cleanup policy for each owned entity;
- session replay-evidence retention and hard maximum age;
- absence of plaintext secret/identity property names in storage documents;
- explicit hash-only fields for credential lookup, credential secret, refresh token, and provider subject;
- Domain assembly has no Infrastructure or Elasticsearch client dependency.

## Deferred implementation

FIN-85 does not:

- add the official Elasticsearch .NET client;
- create index templates or aliases against a live cluster;
- implement repositories;
- implement encryption/HMAC/password hashing;
- implement registration, login, token rotation, provider validation, or events.

Those capabilities are implemented only after API contracts and use-case rules are defined.
