# Identity Service Baseline

## Purpose

The Identity Service is the authoritative service for user authentication credentials, external identity links, and session lifecycle in Financial Assistant.

FIN-74 originally established project and dependency boundaries. Subsequent
increments implemented account creation, sign-in, JWT/refresh-session lifecycle,
Google/Apple validation, phone challenge logic and outbox-backed event publishing.
This page preserves layer ownership, not the old skeleton-only status.
See [API contracts](identity-api-contracts.md), [sessions](identity-session-lifecycle.md)
and [events](identity-event-publishing.md).

## Component boundaries

### API

Responsibilities:

- host internal identity REST endpoints;
- expose health and local OpenAPI endpoints;
- validate transport-level input;
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

- implement service-owned persistence adapters behind application interfaces;
- implement password and token hashing adapters;
- implement JWT signing and validation adapters;
- implement RabbitMQ event publishing;
- bind runtime configuration.

Current wiring includes in-memory account/session/challenge/outbox stores,
password hashing, JWT validation and an event dispatcher. RabbitMQ transport is
configuration-selected; the phone delivery provider is disabled by default.
These adapters do not establish durable production operation.

### Contracts

Responsibilities:

- public request and response contracts;
- versioned identity event contracts;
- transport-safe error conventions.

Contracts must not expose Elasticsearch document metadata, password hashes, refresh token hashes, provider secrets, or internal configuration fields.

## Data ownership

Identity Service owns:

- account identity records;
- credential metadata and password hashes;
- refresh-session records and token hashes;
- external provider links;
- identity lifecycle event publication.

Profile Service owns non-authentication user profile data. Other services consume safe user identifiers through APIs or events and must not read Identity Service indices directly.

FIN-85 records the earlier Elasticsearch models/aliases. Their ownership and
compatibility remain relevant, but [current storage policy](../architecture/storage-policy.md)
prefers service-owned PostgreSQL for durable authoritative state. No durable
adapter migration is claimed here.

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

## Delivered Baseline References

1. FIN-85 — identity data model and owned storage.
2. FIN-86 — client-facing identity API contracts.
3. FIN-75 — email registration and login.
4. FIN-76 — access and refresh token lifecycle.
5. FIN-77 — identity event publishing.
