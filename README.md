# Financial Assistant

Repository foundation for the Financial Assistant product.

Financial Assistant is an intelligent personal finance assistant for mobile, web/admin, and backend services. The backend is based on .NET 8 and uses a pragmatic microservice-oriented structure with clear service ownership boundaries.

## Repository layout

```text
backend/       .NET 8 backend services, service template, shared building blocks, contracts
mobile/        React Native mobile application workspace
web-admin/     Web/Admin UI workspace
infra/         Local platform, Docker Compose, deployment, and infrastructure assets
docs/          Engineering documentation and local development guides
.github/       CI workflows and repository automation
```

## Backend service template

New backend services should be created from:

```text
backend/templates/service-template/
```

The template uses Clean Architecture style layers:

```text
ServiceTemplate.Api/
ServiceTemplate.Application/
ServiceTemplate.Domain/
ServiceTemplate.Infrastructure/
ServiceTemplate.Contracts/
```

Layer responsibility:

| Layer | Responsibility |
|---|---|
| Api | REST endpoints, request/response boundary, auth/correlation middleware. |
| Application | Use cases, commands, queries, validation orchestration. |
| Domain | Domain entities, value objects, business rules. |
| Infrastructure | Elasticsearch, RabbitMQ, external providers, file/object storage, technical adapters. |
| Contracts | Public/internal DTOs, event contracts, integration models. |

## Shared backend folders

```text
backend/shared/building-blocks/
backend/shared/contracts/
```

Rules:

- shared building blocks are technical utilities only;
- shared contracts contain stable cross-service contracts;
- no shared business repository is allowed;
- services must not read another service-owned Elasticsearch index directly.

## Local startup flow

Initial local development flow:

1. Install prerequisites:
   - .NET 8 SDK;
   - Docker Desktop;
   - Node.js LTS;
   - package manager for mobile/web workspaces.
2. Start local infrastructure from `infra/docker-compose/`.
3. Create a backend service from `backend/templates/service-template/`.
4. Configure service-owned Elasticsearch aliases and RabbitMQ settings.
5. Run backend service locally.
6. Run mobile or web-admin workspace when client apps are initialized.

Start local infrastructure:

```bash
cd infra/docker-compose
cp .env.example .env
docker compose pull
docker compose up -d
docker compose ps
```

Or use the helper script:

```bash
cd infra/docker-compose
./scripts/up.sh
```

Health check:

```bash
cd infra/docker-compose
./scripts/healthcheck.sh
```

Stop services:

```bash
cd infra/docker-compose
docker compose down
```

Reset local data:

```bash
cd infra/docker-compose
docker compose down -v
```

Full local infrastructure guide:

```text
infra/docker-compose/README.md
```

## CI

Backend CI is defined in:

```text
.github/workflows/backend-ci.yml
```

The initial CI baseline includes:

- .NET SDK setup;
- `dotnet restore`;
- `dotnet build --no-restore --configuration Release`;
- `dotnet test --no-build --configuration Release`;
- TRX test result artifacts;
- `dotnet format --verify-no-changes`.

Run core checks locally:

```bash
dotnet restore
dotnet build --no-restore --configuration Release
dotnet test --no-build --configuration Release --logger trx --results-directory TestResults
dotnet format --verify-no-changes --verbosity diagnostic
```

Full CI guide:

```text
docs/engineering/ci.md
```

## Architecture guardrails

- Backend: .NET 8.
- API: public REST API through API Gateway.
- Async integration: RabbitMQ events.
- Storage: Elasticsearch-first, service-owned index namespaces.
- AI/OCR: assist with parsing and UX, never source of financial truth.
- Financial truth: deterministic backend logic plus explicit user confirmation.
- Redis: disposable cache and short-lived state, not source of truth.
- MinIO: binary object storage for receipts/files, not domain storage.
- Prometheus/Grafana: observability utilities, not business reporting sources.
