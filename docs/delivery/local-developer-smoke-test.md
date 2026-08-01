# Local Developer Smoke Test

Use this checklist after a fresh checkout or after changing the platform
foundation. It verifies the repository, service template, local infrastructure,
backend solution, tests, documentation, and one sample HTTP host.

The smoke test uses only local development infrastructure and synthetic
configuration. It must not use production secrets, production endpoints, real
identities, real financial data, receipts, OCR content, LLM content, or paid
external providers.

## Pass criteria

The smoke test passes only when:

- the checkout starts and ends clean;
- required tool commands are available;
- Docker Compose configuration validates and all six local services are healthy;
- the backend solution restores, builds, tests, and passes formatting;
- the service template builds through the root solution;
- the Public API Gateway health and info endpoints respond locally;
- required documentation entry points exist;
- no secret or generated output is tracked.

Record the command, outcome, and first failing step in the Jira or pull request
evidence. Do not paste credentials, environment-file contents, financial data,
or container logs containing sensitive values.

## 1. Verify the checkout

Run from the repository root:

```bash
git fetch origin
git switch main
git pull --ff-only
git status --short
```

Expected result: `main` is current and `git status --short` prints nothing.
Do not continue from an uncommitted or unrelated branch state.

## 2. Verify required tools

```bash
git --version
dotnet --version
docker version
docker compose version
node --version
npm --version
```

Expected result: every command exits successfully, `.NET 8` is selected, and
the Docker daemon is reachable. See
[Developer Onboarding](developer-onboarding.md#2-required-tools) when a tool is
missing or the wrong SDK is selected.

## 3. Start and verify local infrastructure

Create the ignored development environment file and validate the resolved
Compose model before starting containers.

### PowerShell

```powershell
Set-Location infra/docker-compose
if (-not (Test-Path .env)) { Copy-Item .env.example .env }
docker compose config --quiet
docker compose pull
docker compose up -d
docker compose ps
```

### Bash, Git Bash, or WSL

```bash
cd infra/docker-compose
test -f .env || cp .env.example .env
docker compose config --quiet
docker compose pull
docker compose up -d
docker compose ps
bash scripts/healthcheck.sh
```

On PowerShell, run the equivalent health checks:

```powershell
Invoke-RestMethod http://localhost:9200 | Out-Null
Invoke-RestMethod "http://localhost:9200/_cluster/health" | Out-Null
Invoke-WebRequest http://localhost:9000/minio/health/live -UseBasicParsing | Out-Null
Invoke-WebRequest http://localhost:9090/-/healthy -UseBasicParsing | Out-Null
Invoke-RestMethod http://localhost:3000/api/health | Out-Null
docker compose exec -T redis redis-cli ping
docker compose exec -T rabbitmq rabbitmq-diagnostics -q ping
```

Expected result:

- `elasticsearch`, `rabbitmq`, `redis`, `minio`, `prometheus`, and
  `grafana` are running;
- HTTP checks return success;
- Redis returns `PONG`;
- RabbitMQ returns a successful ping.

Return to the repository root before continuing:

```powershell
Set-Location ../..
```

```bash
cd ../..
```

For startup failures, port conflicts, service-specific log commands, and safe
reset guidance, use the
[Docker Compose troubleshooting guide](../../infra/docker-compose/README.md#troubleshooting).
Never commit `infra/docker-compose/.env`.

## 4. Verify backend and service template

Run from the repository root:

```bash
dotnet --info
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release --logger trx --results-directory TestResults
dotnet format FinancialAssistant.Backend.sln --verify-no-changes --verbosity diagnostic
```

Expected result: every command exits with code zero. The root solution includes
the reusable service template and repository regression tests, so this verifies
both the template build and its architecture/cross-cutting contracts.

If a command fails, reproduce the first failing step and use the
[CI failure guide](../engineering/ci.md#failure-guide). Template-specific
expectations are documented in the
[service template README](../../backend/templates/service-template/README.md).

## 5. Verify a sample service health endpoint

Start the Public API Gateway with a fixed local URL in one terminal.

### PowerShell

```powershell
$env:ASPNETCORE_URLS = "http://127.0.0.1:5080"
dotnet run --no-build --configuration Release --project backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj
```

### Bash, Git Bash, or WSL

```bash
ASPNETCORE_URLS=http://127.0.0.1:5080 \
  dotnet run --no-build --configuration Release \
  --project backend/gateways/public-api-gateway/FinancialAssistant.PublicApiGateway/FinancialAssistant.PublicApiGateway.csproj
```

In another terminal, verify:

```bash
curl -fsS http://127.0.0.1:5080/health
curl -fsS http://127.0.0.1:5080/gateway/info
```

PowerShell equivalent:

```powershell
Invoke-WebRequest http://127.0.0.1:5080/health -UseBasicParsing
Invoke-RestMethod http://127.0.0.1:5080/gateway/info
```

Expected result: `/health` succeeds and `/gateway/info` reports
`financial-assistant-public-api-gateway` with `status` equal to `running`.
Stop the host with `Ctrl+C`.

## 6. Verify documentation entry points

### PowerShell

```powershell
@(
  "README.md",
  "docs/delivery/developer-onboarding.md",
  "docs/delivery/local-developer-smoke-test.md",
  "docs/engineering/contributing.md",
  "docs/engineering/ci.md",
  "infra/docker-compose/README.md",
  "backend/templates/service-template/README.md"
) | ForEach-Object {
  if (-not (Test-Path $_)) { throw "Missing required documentation: $_" }
}
```

### Bash, Git Bash, or WSL

```bash
for path in \
  README.md \
  docs/delivery/developer-onboarding.md \
  docs/delivery/local-developer-smoke-test.md \
  docs/engineering/contributing.md \
  docs/engineering/ci.md \
  infra/docker-compose/README.md \
  backend/templates/service-template/README.md
do
  test -f "$path" || { echo "Missing required documentation: $path"; exit 1; }
done
```

## 7. Finish cleanly

Stop local infrastructure without deleting volumes:

```bash
cd infra/docker-compose
docker compose down
cd ../..
git status --short
```

Expected result: containers stop and `git status --short` prints nothing.
Use `docker compose down -v` only for an intentional local data reset.

A failure is not a reason to bypass a check or point the application at a
production service. Capture the first failed command, consult the linked guide,
correct the local configuration or implementation, and rerun the checklist from
that step.
