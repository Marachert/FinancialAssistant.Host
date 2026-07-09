# Financial Assistant

Financial Assistant is an intelligent personal finance assistant for Android, iOS, Web, and backend services.

It is not just an expense tracker. The product is designed to minimize manual input through free-form money entry, receipt upload, OCR, AI-assisted classification, recommendations, limits, reporting, notifications, financial score, and educational progress.

## Start here

New contributors should follow the canonical onboarding guide:

```text
docs/delivery/developer-onboarding.md
```

Related guides:

```text
infra/docker-compose/README.md       Local infrastructure and troubleshooting
docs/engineering/contributing.md    Branch, PR, review, and CI workflow
docs/engineering/ci.md              CI checks and quality gates
docs/README.md                       Documentation map
```

## Architecture at a glance

| Area | Baseline |
| --- | --- |
| Backend | .NET 8 pragmatic microservices |
| Client API | Public REST API through the Public API Gateway |
| Async integration | RabbitMQ events |
| Operational storage | Elasticsearch-first, service-owned indices and aliases |
| Cache | Redis for disposable cache and short-lived state |
| File storage | MinIO for receipt and file binaries |
| Mobile | React Native boundary for Android and iOS |
| Web admin | Internal monitoring UI boundary |
| AI and OCR | External providers used for parsing, explanations, recommendations, and UX assistance |

Core rules:

- backend deterministic logic is authoritative for transactions, balances, limits, reports, and scores;
- OCR and LLM output is probabilistic input and must be validated before becoming confirmed financial data;
- each service owns its domain model, repositories, persistence mappings, Elasticsearch indices, and business events;
- clients call backend capabilities through the Public API Gateway;
- shared code contains technical utilities and stable contracts, never shared financial business rules;
- examples, fixtures, logs, and documentation use synthetic data only.

## Repository layout

```text
backend/
  gateways/                        Public API Gateway hosts
  services/                        Service-owned backend capabilities
  shared/
    building-blocks/               Technical cross-cutting utilities
    contracts/                     Stable versioned integration contracts
    elasticsearch/                 Low-level Elasticsearch helpers
    testing/                       Deterministic test helpers and synthetic fixtures
  templates/service-template/      Reusable .NET 8 service template

mobile/app-react-native/            Android/iOS application boundary
web-admin/monitoring-ui/            Internal monitoring UI boundary
infra/docker-compose/               Local Elasticsearch, RabbitMQ, Redis, MinIO, Prometheus, Grafana
docs/                               Architecture, API, events, security, engineering, and delivery guides
tests/                              Repository and backend tests
.github/workflows/                  CI workflows
```

The canonical gateway root is `backend/gateways/`. Do not create a parallel `backend/gateway/` directory.

## Prerequisites

Required for the current backend and local infrastructure baseline:

- Git;
- .NET 8 SDK;
- Docker with Compose v2;
- Node.js LTS;
- npm or another approved JavaScript package manager.

Mobile development additionally requires Android Studio/JDK/Android SDK for Android and macOS/Xcode/CocoaPods for iOS when the React Native scaffold is implemented.

Verify the core tools:

```bash
git --version
dotnet --version
docker version
docker compose version
node --version
npm --version
```

## First local setup

Clone the repository:

```bash
git clone https://github.com/Marachert/FinancialAssistant.Host.git
cd FinancialAssistant.Host
```

Configure and start local infrastructure:

### Bash, Git Bash, or WSL

```bash
cd infra/docker-compose
cp .env.example .env
docker compose config
docker compose pull
docker compose up -d
docker compose ps
```

### PowerShell

```powershell
Set-Location infra/docker-compose
Copy-Item .env.example .env
docker compose config
docker compose pull
docker compose up -d
docker compose ps
```

Never commit `.env`.

Run the infrastructure health check on Bash-compatible environments:

```bash
cd infra/docker-compose
bash scripts/healthcheck.sh
```

Stop services:

```bash
cd infra/docker-compose
docker compose down
```

Reset local volumes only when a clean restart is intended:

```bash
cd infra/docker-compose
docker compose down -v
```

## Backend verification

Run from the repository root:

```bash
dotnet --info
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release --logger trx --results-directory TestResults
dotnet format FinancialAssistant.Backend.sln --verify-no-changes --verbosity diagnostic
```

The same solution is used by Backend CI.

## Public API Gateway

Project:

```text
backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/
```

Run locally:

```bash
dotnet run --project backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj
```

Initial endpoints:

```text
GET /
GET /health
GET /gateway/info
```

Use the local URL printed by `dotnet run` when testing the endpoints.

The gateway is a technical perimeter. It must not own financial business logic, publish domain events for proxied requests, or access service-owned storage directly.

## Backend service template

New backend services should follow:

```text
backend/templates/service-template/
```

Template layers:

```text
ServiceTemplate.Api/
ServiceTemplate.Application/
ServiceTemplate.Domain/
ServiceTemplate.Infrastructure/
ServiceTemplate.Contracts/
```

| Layer | Responsibility |
| --- | --- |
| Api | REST endpoints, authentication boundary, correlation, request/response mapping |
| Application | Use cases, commands, queries, validation orchestration |
| Domain | Entities, value objects, invariants, deterministic business rules |
| Infrastructure | Elasticsearch, RabbitMQ, external providers, object storage, technical adapters |
| Contracts | DTOs and versioned integration contracts |

Dependency direction remains inward. Infrastructure implements adapters; Domain must not depend on infrastructure or provider SDKs.

## Local infrastructure

The Docker Compose baseline provides:

| Service | Local endpoint | Role |
| --- | --- | --- |
| Elasticsearch | `http://localhost:9200` | Service-owned operational indices |
| RabbitMQ | `localhost:5672`, UI `http://localhost:15672` | Async event bus |
| Redis | `localhost:6379` | Disposable cache and short-lived state |
| MinIO | API `http://localhost:9000`, UI `http://localhost:9001` | Receipt/file binaries |
| Prometheus | `http://localhost:9090` | Local metrics |
| Grafana | `http://localhost:3000` | Local dashboards |

These are technical utilities. They do not create shared ownership of business data.

## Contributor workflow

Recommended flow:

1. Select one Jira issue or one small logical change.
2. Update local `main`.
3. Create a focused branch such as `feature/FIN-123-short-description`, `fix/FIN-123-short-description`, `docs/FIN-123-short-description`, or `codex/fin-123-short-description`.
4. Implement the change and update relevant documentation.
5. Run local build, tests, formatting, and infrastructure validation where applicable.
6. Open a pull request referencing the Jira issue.
7. Wait for CI and process all review comments.
8. Resolve review threads only after the final updated CI run is green.
9. Merge through the repository owner workflow.

Detailed contributor rules:

```text
docs/engineering/contributing.md
```

## CI quality gates

Backend CI is defined in:

```text
.github/workflows/backend-ci.yml
```

Current checks:

- restore the selected .NET solution/project;
- build in Release configuration;
- run test projects and upload TRX results;
- verify formatting with `dotnet format --verify-no-changes`;
- run repository structure, onboarding, and hygiene regression tests.

CI details and branch protection recommendations:

```text
docs/engineering/ci.md
```

## Security and privacy

Do not commit or expose:

- `.env` files or production configuration;
- tokens, API keys, passwords, private keys, certificates, or signing artifacts;
- real user identities or personal financial data;
- real receipts, raw OCR text, or real LLM prompts/responses;
- generated Docker volumes, logs, package output, `bin`, `obj`, build results, or test results.

Use synthetic fixtures and privacy-safe logs. LLM and OCR providers are never sources of financial truth.

## Documentation map

| Folder | Responsibility |
| --- | --- |
| `docs/architecture/` | System boundaries, ownership, diagrams, and architecture decisions |
| `docs/api/` | REST and integration API contracts |
| `docs/events/` | RabbitMQ event contracts and delivery conventions |
| `docs/security/` | Security, privacy, abuse protection, and operational safety |
| `docs/delivery/` | Onboarding, release readiness, implementation sequencing, and evidence |
| `docs/engineering/` | Detailed implementation, CI, and contributor guides |
| `docs/reviews/` | Review and acceptance evidence |

## Current onboarding target

A new contributor should be able to:

- understand the product and ownership model;
- validate required tools;
- start the local Docker Compose stack;
- restore, build, test, and format `FinancialAssistant.Backend.sln`;
- run and verify the Public API Gateway;
- create a focused Jira branch and pull request;
- process CI and review feedback without exposing sensitive data.

Follow `docs/delivery/developer-onboarding.md` for the complete checklist.
