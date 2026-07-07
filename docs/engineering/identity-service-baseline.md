# Identity Service Baseline

## Purpose

The Identity Service is the authoritative service for user authentication credentials, external identity links, and session lifecycle in Financial Assistant.

FIN-74 establishes project and dependency boundaries only. It does not implement account creation, login, token issuance, provider validation, or storage schemas.

## Component boundaries

### API

Responsibilities:

- host internal identity REST endpoints;
- expose health and local OpenAPI endpoints;
- validate transport-level input in later tasks;
- compose Application and Infrastructure dependencies;
- return safe public errors through the gateway.

Must not:

- contain password hashing or token logic;
- access Elasticsearch directly from endpoint handlers;
- publish RabbitMQ messages directly from endpoint handlers;
- expose storage documents as API responses.

### Application

Responsibilities:

- orchestrate registration, sign-in, refresh, logout, and provider-linking use cases;
- define interfaces for storage, token, clock, hashing, and event adapters;
- enforce deterministic authentication workflows.

The Application layer depends on Domain and Contracts, not Infrastructure.

### Domain

Responsibilities:

- identity invariants;
- account and session lifecycle rules;
- provider-linking rules;
- deterministic security decisions that do not depend on transport or storage SDKs.

The Domain project has no Infrastructure or API dependency.

### Infrastructure

Responsibilities:

- implement Elasticsearch repositories owned by Identity Service;
- implement password and token hashing adapters;
- implement JWT signing and validation adapters;
- implement RabbitMQ event publishing;
- bind runtime configuration.

FIN-74 provides only safe configuration and event-publishing placeholders. Active adapters are added in later tasks.

### Contracts

Responsibilities:

- public request and response contracts;
- versioned identity event contracts;
- transport-safe error conventions.

Contracts must not expose Elasticsearch document metadata, password hashes, refresh token hashes, provider secrets, or internal configuration fields.

## Data ownership

Identity Service will own:

- account identity records;
- credential metadata and password hashes;
- refresh-session records and token hashes;
- external provider links;
- identity lifecycle event publication.

Profile Service owns non-authentication user profile data. Other services consume safe user identifiers through APIs or events and must not read Identity Service indices directly.

Exact document models, aliases, mappings, retention, and cleanup rules are deferred to FIN-85.

## Synchronous and asynchronous flows

Synchronous REST is used for:

- registration;
- sign-in;
- token refresh;
- logout;
- current identity context;
- provider validation and linking.

RabbitMQ is used after authoritative state changes for versioned lifecycle events such as account registration or session revocation. Event publishing is not the source of truth and must not make authentication calculations probabilistic.

## Baseline runtime behavior

The service exposes:

- process health;
- liveness;
- configuration readiness;
- a safe technical service information endpoint;
- OpenAPI only in Development and Testing.

Readiness checks validate that the baseline configuration is structurally present. They do not claim Elasticsearch or RabbitMQ connectivity before those integrations exist.

## Security rules

- passwords are never stored or logged in plaintext;
- access and refresh tokens are never logged;
- refresh token material is stored only as a hash;
- verification codes and provider credentials are never logged;
- public errors must not reveal whether an account exists;
- diagnostic endpoints expose no user, credential, token, provider, or storage-address data;
- LLM and OCR are outside the identity trust boundary.

## Follow-up implementation order

1. FIN-85 — identity data model and owned storage.
2. FIN-86 — client-facing identity API contracts.
3. FIN-75 — email registration and login.
4. FIN-76 — access and refresh token lifecycle.
5. FIN-77 — identity event publishing.
