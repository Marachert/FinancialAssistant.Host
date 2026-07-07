# Financial Assistant Identity Service

Initial .NET 8 Identity Service baseline for FIN-16.

Canonical engineering documentation:

```text
docs/engineering/identity-service-baseline.md
```

## Responsibility

Identity Service owns authentication credentials, provider links, refresh sessions, and identity lifecycle events.

It does not own the user financial profile, transaction data, categories, receipts, analytics, scores, recommendations, or notification preferences. Other services must use Identity Service APIs or versioned events and must not read Identity Service storage directly.

The API Gateway remains the public entry point. It may validate or forward safe identity context, but it must not persist credentials or refresh sessions.

## Project structure

```text
FinancialAssistant.Identity.Api
FinancialAssistant.Identity.Application
FinancialAssistant.Identity.Domain
FinancialAssistant.Identity.Infrastructure
FinancialAssistant.Identity.Contracts
```

Layer rules:

- `Domain` contains identity business rules and has no infrastructure dependency.
- `Application` orchestrates identity use cases and depends on Domain and Contracts.
- `Infrastructure` implements storage and messaging adapters behind application abstractions.
- `Contracts` contains public and event contracts without storage-specific fields.
- `Api` hosts REST endpoints, health checks, OpenAPI, and composition only.

FIN-74 intentionally contains no registration, login, token, or provider business flow. Those are delivered by FIN-75, FIN-76, FIN-77, FIN-85, and FIN-86.

## Current endpoints

```text
GET /
GET /health
GET /health/live
GET /health/ready
GET /identity/info
GET /openapi/v1.json   # Development and Testing only
```

`/identity/info` exposes a safe technical summary only. It must never expose credentials, tokens, provider identifiers, storage aliases, connection addresses, or user data.

## Configuration placeholders

```text
Identity:ServiceName
Identity:Storage:Provider
Identity:Storage:AccountsAlias
Identity:Storage:SessionsAlias
Identity:Events:Mode
Identity:Events:Exchange
```

The storage provider is declared as Elasticsearch, but this baseline does not create a client or access an index. Exact aliases, mappings, retention, and cleanup rules belong to FIN-85.

Event publishing uses a no-op adapter in `placeholder` mode. RabbitMQ integration and versioned identity events belong to FIN-77.

Secrets, passwords, token material, verification codes, and provider credentials must never be committed to configuration files or written to logs.

## Build and run

From the repository root:

```bash
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet run --project backend/services/identity/FinancialAssistant.Identity.Api/FinancialAssistant.Identity.Api.csproj
```

Then verify using the actual local ASP.NET Core URL:

```bash
curl -i http://localhost:5000/health
curl -i http://localhost:5000/health/live
curl -i http://localhost:5000/health/ready
curl -i http://localhost:5000/identity/info
curl -i http://localhost:5000/openapi/v1.json
```

## Boundary checklist

- Domain has no project reference to Infrastructure or API.
- API contains no credential or session persistence logic.
- Infrastructure contains placeholders only and no active Elasticsearch or RabbitMQ client.
- No plaintext passwords, refresh tokens, access tokens, verification codes, or provider secrets are stored or logged.
- LLM and OCR are not involved in authentication decisions or identity persistence.
