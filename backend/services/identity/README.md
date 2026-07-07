# Financial Assistant Identity Service

.NET 8 Identity Service for FIN-16.

Canonical engineering documentation:

```text
docs/engineering/identity-service-baseline.md
docs/engineering/identity-data-model-and-storage.md
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
- `Infrastructure` contains storage documents, index catalog, cleanup policy, and future adapters.
- `Contracts` contains public and event contracts without storage-specific fields.
- `Api` hosts REST endpoints, health checks, OpenAPI, and composition only.

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

Example:

```text
fa-dev-identity-accounts-v1-000001
```

Stable aliases:

```text
fa-dev-identity-accounts-read
fa-dev-identity-accounts-write
```

Only Identity Service credentials may access this namespace. Direct cross-service index reads are forbidden.

Storage documents never contain raw passwords, access tokens, refresh tokens, email addresses, phone numbers, verification codes, reset tokens, or raw provider subjects. Exact lookup values and secrets are represented by purpose-specific hashes.

FIN-85 defines naming, documents, and cleanup policy only. It does not activate an Elasticsearch client or create indices.

## Runtime endpoints

```text
GET /
GET /health
GET /health/live
GET /health/ready
GET /identity/info
GET /openapi/v1.json   # Development and Testing only
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
Identity:Events:Mode
Identity:Events:Exchange
```

Secrets and Elasticsearch credentials belong in environment variables or a secret manager, never committed configuration.

## Build and test

From the repository root:

```bash
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
dotnet run --project backend/services/identity/FinancialAssistant.Identity.Api/FinancialAssistant.Identity.Api.csproj
```

## Boundary checklist

- Domain has no project reference to Infrastructure or API.
- Storage documents live in Infrastructure rather than Domain or Contracts.
- No active Elasticsearch or RabbitMQ client is introduced by FIN-85.
- No plaintext identity secrets or lookup values are stored.
- Direct cross-service index reads are forbidden.
- Cleanup is owned by Identity Service; ILM handles rollover/generation lifecycle, not domain deletion decisions.
- LLM and OCR are not involved in authentication or identity persistence.
