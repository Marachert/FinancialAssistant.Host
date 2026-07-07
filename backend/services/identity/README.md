# Financial Assistant Identity Service

.NET 8 Identity Service for FIN-16.

Canonical engineering documentation:

```text
docs/engineering/identity-service-baseline.md
docs/engineering/identity-data-model-and-storage.md
docs/engineering/identity-api-contracts.md
docs/engineering/identity-email-registration-login.md
```

## Responsibility

Identity Service owns authentication credentials, provider links, refresh sessions, and identity lifecycle events.

It does not own profile, transaction, category, receipt, analytics, score, recommendation, or notification data. Other services use Identity Service APIs or versioned events and must not read Identity Service indices directly.

## Project structure

```text
FinancialAssistant.Identity.Api
FinancialAssistant.Identity.Application
FinancialAssistant.Identity.Domain
FinancialAssistant.Identity.Infrastructure
FinancialAssistant.Identity.Contracts
FinancialAssistant.Identity.Tests
```

Layer rules:

- `Domain` contains identity rules and has no infrastructure dependency.
- `Application` orchestrates use cases and depends on Domain and Contracts.
- `Infrastructure` contains storage documents, index catalog, cleanup policy, and adapters.
- `Contracts` contains versioned client and event contracts without storage-specific fields.
- `Api` hosts REST endpoints, health checks, OpenAPI, and composition only.

## Versioned client API

```text
POST /auth/v1/register
POST /auth/v1/sign-in
POST /auth/v1/refresh
POST /auth/v1/logout
GET  /auth/v1/me
```

The Public API Gateway owns `/auth` and forwards these paths to Identity Service.

FIN-75 activates registration and sign-in. FIN-76 owns refresh, logout, access-token validation, session rotation/revocation, replay detection, and current-user context.

Registration accepts optional `Idempotency-Key`. Logout and current-user contracts are marked as Bearer-protected in OpenAPI. Errors use `application/problem+json` with stable machine-readable codes and a safe trace ID.

Public DTOs never expose Elasticsearch names, schema versions, sequence numbers, hashes, or other persistence fields.

## Email registration and sign-in

Registration:

- validates email, password policy, client context, and idempotency-key shape;
- normalizes email in memory;
- derives a keyed lookup value instead of storing raw email;
- detects duplicates atomically through `IIdentityAccountStore`;
- protects credentials with ASP.NET Core Identity password hashing;
- creates an active account with the `user` role;
- issues initial opaque session values;
- publishes `user.registered.v1` through `IIdentityEventPublisher`.

Sign-in performs keyed lookup, account-state validation, protected credential verification, optional rehash upgrade, and generic public failure handling. Unknown email and wrong password return the same `authentication_failed` response.

The current `InMemoryDevelopment` store and opaque initial session issuer make these flows executable in local development and CI. They are not production persistence/session-lifecycle implementations: state is not durable or shared across replicas, and refresh/validation/revocation remain FIN-76.

## Owned Elasticsearch storage

FIN-85 defines four service-owned entities:

```text
accounts
credentials
sessions
external-identities
```

Physical index convention:

```text
fa-{environment}-identity-{entity}-v{schemaVersion}-{generation}
```

Stable aliases:

```text
fa-dev-identity-accounts-read
fa-dev-identity-accounts-write
```

Only Identity Service credentials may access this namespace. Direct cross-service index reads are forbidden.

Storage documents use purpose-specific hashes and never contain raw passwords, access tokens, refresh tokens, email addresses, phone numbers, verification codes, reset tokens, or raw provider subjects.

## Runtime endpoints

```text
GET  /
GET  /health
GET  /health/live
GET  /health/ready
GET  /identity/info
GET  /openapi/v1.json   # Development and Testing only
POST /auth/v1/register  # active in FIN-75
POST /auth/v1/sign-in   # active in FIN-75
POST /auth/v1/refresh   # 501 until FIN-76
POST /auth/v1/logout    # 501 until FIN-76
GET  /auth/v1/me        # 501 until FIN-76
```

## Configuration

```text
Identity:ServiceName
Identity:Storage:Provider
Identity:Storage:Environment
Identity:Storage:SchemaVersion
Identity:Storage:InitialGeneration
Identity:Storage:Cleanup:DeletedAccountRetentionDays
Identity:Storage:Cleanup:RemovedCredentialRetentionDays
Identity:Storage:Cleanup:TerminalSessionRetentionDays
Identity:Storage:Cleanup:HardMaximumSessionDocumentDays
Identity:Storage:Cleanup:RemovedProviderLinkRetentionDays
Identity:Authentication:RuntimeAdapter
Identity:Authentication:LookupHmacKey
Identity:Authentication:AccessTokenLifetimeMinutes
Identity:Authentication:RefreshTokenLifetimeDays
Identity:Events:Mode
Identity:Events:Exchange
```

`LookupHmacKey`, Elasticsearch credentials, and all other secrets belong in environment variables or a secret manager, never committed configuration. When the key is absent, the development adapter generates an ephemeral process-local key.

## Build and test

From the repository root:

```bash
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
dotnet run --project backend/services/identity/FinancialAssistant.Identity.Api/FinancialAssistant.Identity.Api.csproj
```

OpenAPI:

```text
http://localhost:5000/openapi/v1.json
```

The `.http` file contains synthetic-only examples for every v1 operation.

## Boundary checklist

- Domain has no project reference to Infrastructure or API.
- Application depends on storage, hashing, session, clock, and event abstractions.
- Storage documents live in Infrastructure rather than Domain or Contracts.
- Public contracts contain no storage implementation fields.
- Plaintext credentials and raw email are not persisted or logged by FIN-75.
- Direct cross-service index reads are forbidden.
- Registration and sign-in calculations remain deterministic server logic.
- Production Elasticsearch persistence, complete session lifecycle, and RabbitMQ delivery remain separate adapters/increments.
- LLM and OCR are not involved in authentication or identity persistence.
