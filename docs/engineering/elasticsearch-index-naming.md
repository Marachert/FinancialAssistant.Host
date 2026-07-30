# Elasticsearch Index Naming and Alias Conventions

Related Jira: FIN-59.

This guide defines the canonical naming and ownership contract for local and
future production Elasticsearch indices. It applies to service-owned operational
data, read models, audit data, and monitoring data.

## Ownership

Each backend service owns its Elasticsearch namespace, physical indices,
templates, mappings, migrations, aliases, retention rules, credentials, and
data-access policy.

Only the owning service may read or write its indices. Other services obtain
data through the owner's REST API or versioned integration events. Shared
Elasticsearch code may provide low-level client, validation, retry, health, or
serialization helpers, but it must not expose another service's documents or
create cross-service repositories.

Elasticsearch remains a service-owned operational store. It does not make
probabilistic OCR or LLM output authoritative, and it does not replace
deterministic backend financial rules.

## Segment Grammar

Every variable segment uses lowercase ASCII letters, digits, and single hyphen
separators:

```text
[a-z0-9]+(?:-[a-z0-9]+)*
```

Do not use spaces, underscores, uppercase letters, tenant names, user
identifiers, email addresses, or other personal or financial data in an index
or alias name.

Canonical environments are `local`, `dev`, `test`, `staging`, and
`prod`. A deployment may define another environment segment only when it
follows the same grammar and cannot collide with an existing environment.

## Canonical Names

Physical index:

```text
fa-{environment}-{service}-{entity}-v{schemaVersion}-{generation}
```

Index template pattern:

```text
fa-{environment}-{service}-{entity}-v{schemaVersion}-*
```

Stable aliases:

```text
fa-{environment}-{service}-{entity}-read
fa-{environment}-{service}-{entity}-write
```

Rules:

- `schemaVersion` is a positive integer prefixed with `v`, such as `v1`;
- `generation` is a positive six-digit sequence, such as `000001`;
- the service segment is the owning capability, not a shared database name;
- the entity segment describes one owned document family;
- application reads use the read alias and application writes use the write
  alias;
- runtime code does not write directly to a physical index;
- wildcard access across service namespaces is forbidden.

The existing Identity Service catalog follows this contract with names such as
`fa-dev-identity-accounts-v1-000001`.

## Version And Generation Lifecycle

Increment the schema version when a mapping or document contract is incompatible
with the current schema. Create a matching versioned template before creating
the first physical generation for that schema.

Increment the generation for rollover, reindexing, or a replacement index that
keeps the same compatible schema. Never reuse a physical index name.

A migration performs these steps:

1. Create the new physical index from the intended versioned template.
2. Backfill and validate only synthetic or authorized service-owned data.
3. Atomically move the read alias after read verification.
4. Atomically move the write alias to exactly one writable generation.
5. Retain or remove the previous generation according to the owning service's
   rollback, retention, and audit policy.

The read alias normally points to the active generation. A migration may
temporarily read multiple compatible generations only when the owning service
defines deduplication and rollback behavior. The write alias always has exactly
one write index.

## Service Examples

These examples use synthetic `dev` names and generation `000001`.

| Capability | Owner namespace and entity | Physical index | Read alias | Write alias |
| --- | --- | --- | --- | --- |
| Auth | `identity/accounts` | `fa-dev-identity-accounts-v1-000001` | `fa-dev-identity-accounts-read` | `fa-dev-identity-accounts-write` |
| Intake | `transaction-intake/drafts` | `fa-dev-transaction-intake-drafts-v1-000001` | `fa-dev-transaction-intake-drafts-read` | `fa-dev-transaction-intake-drafts-write` |
| Income | `income/entries` | `fa-dev-income-entries-v1-000001` | `fa-dev-income-entries-read` | `fa-dev-income-entries-write` |
| Expense | `expense/entries` | `fa-dev-expense-entries-v1-000001` | `fa-dev-expense-entries-read` | `fa-dev-expense-entries-write` |
| Analytics | `analytics/monthly-summaries` | `fa-dev-analytics-monthly-summaries-v1-000001` | `fa-dev-analytics-monthly-summaries-read` | `fa-dev-analytics-monthly-summaries-write` |
| Score | `financial-score/snapshots` | `fa-dev-financial-score-snapshots-v1-000001` | `fa-dev-financial-score-snapshots-read` | `fa-dev-financial-score-snapshots-write` |
| Audit | `audit/events` | `fa-dev-audit-events-v1-000001` | `fa-dev-audit-events-read` | `fa-dev-audit-events-write` |
| Monitoring | `monitoring/service-health` | `fa-dev-monitoring-service-health-v1-000001` | `fa-dev-monitoring-service-health-read` | `fa-dev-monitoring-service-health-write` |

These examples reserve namespaces; they do not create production indices or
grant cross-service access. FIN-60 owns the executable local sample template and
idempotent alias bootstrap.
