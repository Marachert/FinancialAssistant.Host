# Shared Elasticsearch Utilities

This folder contains reusable technical contracts for service-owned Elasticsearch integrations.

## Projects

* `FinancialAssistant.Shared.Elasticsearch` provides the low-level repository, index-name, optimistic-concurrency, and mapping-bootstrap contracts.
* `FinancialAssistant.Shared.Elasticsearch.Tests` verifies the shared contract invariants.

Only a service's Infrastructure project may reference these contracts. Domain and Application projects define capability-specific ports and remain independent of Elasticsearch. Infrastructure adapters implement those ports with service-owned document models and mappings.

## Repository rules

* Reads use `ElasticsearchIndexNames.ReadAlias`.
* Creates, updates, and deletes use `ElasticsearchIndexNames.WriteAlias`.
* Repository callers and implementations must not address a physical generation directly.
* An expected `ElasticsearchConcurrencyToken` maps to Elasticsearch `if_seq_no` and `if_primary_term` parameters.
* A version conflict is returned as an application conflict. Implementations must not blindly retry a stale write or delete.
* Successful reads and writes return the current sequence number and primary term for the next conditional mutation.

`IElasticsearchMappingBootstrap` is implemented inside each owning service. The operation must be idempotent, verify the expected template, mapping, and read/write aliases before mutation, and fail on incompatible drift. It must not silently overwrite an incompatible mapping.

Allowed shared helpers include:

* client registration and typed options helpers;
* retry-safe technical policies;
* index-name validation helpers;
* common serialization conventions;
* health-check adapters;
* testable low-level request utilities.

Not allowed:

* shared business or capability-specific repositories;
* service-owned document models or mappings;
* hard-coded cross-service index access;
* financial calculations or categorization rules;
* analytics read models owned by a specific capability;
* credentials, production endpoints, or index contents.

Each service owns its Elasticsearch indices, aliases, mappings, migrations, repositories, retention rules, credentials, and data access policy. A shared helper may connect to Elasticsearch, but it may not decide what another service stores or expose that service's documents.
