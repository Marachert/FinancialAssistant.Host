# Developer Onboarding Guide

This guide is the canonical first-day path for a developer joining Financial Assistant.

Use it together with:

- `README.md` for the repository overview;
- `infra/docker-compose/README.md` for local infrastructure details;
- `docs/engineering/contributing.md` for the pull request workflow;
- `docs/engineering/ci.md` for CI quality gates.

## 1. Product and architecture context

Financial Assistant is an intelligent personal finance assistant for Android, iOS, Web, and backend services.

The product is designed to minimize manual input through free-form money entry, receipt upload, OCR, AI-assisted classification, recommendations, limits, reporting, notifications, and financial education.

Core engineering rules:

- backend deterministic logic is authoritative for transactions, balances, limits, reports, and scores;
- OCR and LLM output is probabilistic input and must be validated before it becomes confirmed financial data;
- clients call backend capabilities through the Public API Gateway;
- each backend service owns its domain model, persistence mappings, Elasticsearch indices/aliases, repositories, and business events;
- RabbitMQ is used for asynchronous integration, not as hidden shared storage;
- examples, fixtures, logs, and documentation must use synthetic data only.

## 2. Required tools

Install these before cloning the repository:

| Tool | Required for | Verification command |
| --- | --- | --- |
| Git | Source control | `git --version` |
| .NET 8 SDK | Backend build and tests | `dotnet --version` |
| Docker with Compose v2 | Local infrastructure | `docker version` and `docker compose version` |
| Node.js LTS | Mobile and web workspaces | `node --version` |
| npm or another approved package manager | JavaScript dependencies | `npm --version` |

Mobile development also requires platform tooling when the application scaffold is implemented:

- Android: Android Studio, Android SDK, JDK, and an emulator or device;
- iOS: macOS, Xcode, CocoaPods, and an iOS simulator or device.

The canonical mobile boundary already exists at `mobile/app-react-native/`, but dedicated frontend tasks own the React Native scaffold and dependency setup.

## 3. Clone and inspect the repository

```bash
git clone https://github.com/Marachert/FinancialAssistant.Host.git
cd FinancialAssistant.Host
git status
```

Expected primary paths:

```text
backend/gateways/                 Public API Gateway hosts
backend/services/                 Service-owned backend capabilities
backend/shared/                   Technical building blocks and stable contracts
backend/templates/service-template/  Reusable .NET 8 service template
mobile/app-react-native/          Android/iOS client boundary
web-admin/monitoring-ui/          Internal monitoring UI boundary
infra/docker-compose/             Local Elasticsearch/RabbitMQ/Redis/MinIO/monitoring stack
docs/                             Architecture, API, events, security, delivery, and engineering guides
tests/                            Repository and backend test projects
```

Do not create the obsolete singular `backend/gateway/` path. The canonical root is `backend/gateways/`.

## 4. Configure local infrastructure

From the repository root:

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

The local stack exposes:

| Dependency | Endpoint |
| --- | --- |
| Elasticsearch | `http://localhost:9200` |
| RabbitMQ management | `http://localhost:15672` |
| Redis | `localhost:6379` |
| MinIO API / console | `http://localhost:9000` / `http://localhost:9001` |
| Prometheus | `http://localhost:9090` |
| Grafana | `http://localhost:3000` |

Run the provided health check on Bash-compatible environments:

```bash
cd infra/docker-compose
bash scripts/healthcheck.sh
```

Stop the stack:

```bash
docker compose down
```

Remove local volumes only when a clean reset is intended:

```bash
docker compose down -v
```

See `infra/docker-compose/README.md` for service-specific checks and troubleshooting.

## 5. Restore, build, test, and format the backend

Run from the repository root:

```bash
dotnet --info
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release --logger trx --results-directory TestResults
dotnet format FinancialAssistant.Backend.sln --verify-no-changes --verbosity diagnostic
```

Generated `bin`, `obj`, `TestResults`, `BuildResults`, package, mobile build, and local-secret files must remain untracked.

## 6. Run the Public API Gateway

```bash
dotnet run --project backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj
```

Verify the initial endpoints in another terminal:

```bash
curl http://localhost:5000/health
curl http://localhost:5000/gateway/info
```

The actual local URL is printed by `dotnet run`; use that value when it differs from `http://localhost:5000`.

The gateway is a technical perimeter. It does not own financial business logic and must not read service-owned storage directly.

## 7. Understand service ownership before coding

Before adding a backend feature, identify:

1. the business capability that owns the behavior;
2. the service that owns the authoritative data;
3. synchronous REST operations required by clients or other services;
4. asynchronous events published after owned state changes;
5. Elasticsearch indices, aliases, object metadata, cache entries, and queues owned by that service;
6. which calculations must remain deterministic and which UX behavior may use OCR or LLM assistance.

Shared projects may contain technical utilities and stable integration contracts. They must not become shared business repositories, cross-service persistence models, or a place for financial rules.

## 8. Create a branch and make a focused change

Start from current `main`:

```bash
git checkout main
git pull --ff-only
git checkout -b feature/FIN-123-short-description
```

Accepted branch prefixes include `feature/`, `fix/`, `docs/`, and `codex/`.

Keep one Jira issue or one small logical change per pull request.

Before pushing:

```bash
git status
git diff --check
dotnet build FinancialAssistant.Backend.sln --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release
dotnet format FinancialAssistant.Backend.sln --verify-no-changes --verbosity diagnostic
```

For Docker Compose changes, also run:

```bash
cd infra/docker-compose
docker compose config
```

## 9. Pull request and review expectations

A merge-ready pull request includes:

- the Jira issue key;
- a concise scope summary;
- architecture and ownership implications;
- tests and verification evidence;
- documentation updates when behavior or commands change;
- known limitations or follow-up tasks.

Do not merge until applicable CI checks are green and review comments are processed.

For every review finding:

1. evaluate whether it is valid;
2. implement the fix and add regression coverage where practical;
3. wait for the updated CI run;
4. reply with commit and verification evidence;
5. resolve the thread only after the final pipeline is green.

## 10. Security and privacy checklist

Never commit or paste into logs, tests, pull requests, or documentation:

- `.env` files or production configuration;
- passwords, tokens, API keys, signing keys, certificates, or private PEM files;
- real user identities or personal financial data;
- real receipt images or raw OCR output;
- real LLM prompts or responses;
- generated Docker volumes, package output, build artifacts, or test results.

Use synthetic fixtures and sanitized correlation identifiers.

## 11. Common first-day problems

| Problem | First action |
| --- | --- |
| Wrong .NET SDK | Run `dotnet --info`; install .NET 8 SDK |
| Docker daemon unavailable | Start Docker Desktop or the local Docker service |
| Port conflict | Inspect `.env` and stop or remap the conflicting local service |
| Elasticsearch exits | Increase Docker memory and inspect `docker compose logs elasticsearch` |
| Restore/build failure | Run the same failing command locally against `FinancialAssistant.Backend.sln` |
| Formatting failure | Run `dotnet format FinancialAssistant.Backend.sln`, then verify again |
| Tests fail only in CI | Inspect the first failing assertion and downloaded TRX artifact |
| Local secret appears in Git | Remove it from tracking, rotate it if exposed, and extend hygiene rules/tests |

## 12. Onboarding completion checklist

A developer is ready to take a normal Jira task when they can confirm:

- the repository was cloned and `git status` is clean;
- required tool versions are available;
- Docker Compose configuration validates;
- the local infrastructure stack starts and passes health checks;
- `FinancialAssistant.Backend.sln` restores, builds, tests, and passes formatting;
- the Public API Gateway health endpoint responds;
- service ownership and AI/OCR guardrails are understood;
- the contributor and review workflow is understood;
- no production secrets or real financial data were used.
