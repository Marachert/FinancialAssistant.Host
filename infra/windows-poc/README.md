# Windows Server PoC deployment

This folder is the production-like, single-host deployment baseline for the
Financial Assistant controlled PoC. It is separate from `infra/docker-compose`,
which remains the smaller developer infrastructure stack.

The stack uses Linux containers through Docker Engine and Docker Compose v2 on
a Windows Server host. It builds every currently runnable backend API from the
repository, publishes only Nginx to the server network, and keeps infrastructure
administration ports on `127.0.0.1`.

## Topology

```text
mobile/web -> Nginx :8080 -> Public API Gateway :8080 -> service APIs :8080
                                      |
                                      +-> RabbitMQ / Redis / MinIO
                                      +-> Elasticsearch / PostgreSQL
                                      +-> Monitoring / Audit / MCP

host loopback only -> Elasticsearch, RabbitMQ UI, MinIO UI, Prometheus, Grafana
```

The internal Docker network is marked `internal: true`. MCP has no public route.
AI and OCR adapters remain provider-disabled unless a later release ticket adds
reviewed provider configuration; this stack consumes no paid provider credits.

## Host prerequisites

- Windows Server with current security updates;
- Docker Engine capable of running Linux containers;
- Docker Compose v2 available as `docker compose`;
- PowerShell 7 (`pwsh`);
- Git and at least 16 GB RAM available to the PoC stack;
- inbound firewall access only to the chosen reverse-proxy port;
- an operator-owned backup directory outside the repository.

Docker Desktop is not a Windows Server production runtime. Select and license a
supported Linux-container runtime for the target host before controlled use.

## Configuration and secrets

Create the ignored operator file, then replace every `REQUIRED_` placeholder:

```powershell
Set-Location infra/windows-poc
Copy-Item .env.poc.example .env.poc
```

Do not commit `.env.poc`. Restrict its NTFS ACL to the deployment identity and
the authorized operator group. For a longer-lived environment, inject secrets
from the approved host secret store or CI environment rather than retaining the
file. Rotate the gateway, service, receipt-event, monitoring, MCP, token, HMAC,
database, queue, cache, object-store, and Grafana values independently.

RabbitMQ credentials are embedded in internal AMQP URLs, so every required
secret must use Base64URL-safe characters (`A-Z`, `a-z`, `0-9`, `_`, and `-`).
The validation script enforces that alphabet, a minimum length of 32 characters,
and replacement of every template placeholder without printing any value. It
cannot judge entropy; use cryptographically random values.

Backend CI also runs `docker compose config --quiet` against the non-secret
template on every pull request, so syntax, interpolation, anchors, and the
resolved Compose model remain executable release gates.

## Validate and start

Run from the repository root:

```powershell
pwsh -NoProfile -NonInteractive -File infra/windows-poc/scripts/validate.ps1
pwsh -NoProfile -NonInteractive -File infra/windows-poc/scripts/up.ps1
pwsh -NoProfile -NonInteractive -File infra/windows-poc/scripts/verify.ps1
```

The first build publishes each API through the shared multi-stage .NET 8
Dockerfile and can take several minutes. `up.ps1` returns only after Compose
health checks pass. The public checks are:

```text
http://SERVER:8080/reverse-proxy-health
http://SERVER:8080/health
```

Change `POC_HTTP_PORT` when another host port is required. Elasticsearch,
RabbitMQ management, MinIO console, Prometheus, and Grafana are intentionally
bound to loopback; use an authenticated administrative tunnel instead of
opening those ports to the network.

## Reverse proxy and TLS

`nginx/nginx.conf` is the checked-in HTTP reverse-proxy baseline. It logs method
and path but excludes query strings, request bodies, authorization headers, and
financial payloads. Configure the public host name, trusted proxy boundaries,
rate limits, and certificate source for the target environment.

`nginx/tls-server.conf.example` shows the TLS boundary. Certificates and private
keys must be mounted read-only from outside the repository or supplied as Docker
secrets. Never commit them. A Windows IIS reverse proxy may terminate TLS in
front of the Compose Nginx service when that is the approved host standard.

## Stop

```powershell
pwsh -NoProfile -NonInteractive -File infra/windows-poc/scripts/down.ps1
```

The command retains named volumes. Do not use `docker compose down --volumes`
for an environment containing test evidence.

## Backup

Choose an operator-controlled directory outside the repository:

```powershell
pwsh -NoProfile -NonInteractive -File infra/windows-poc/scripts/backup.ps1 `
  -BackupRoot D:\FinancialAssistantBackups
```

The script:

1. validates Docker and the environment without printing secrets;
2. creates an Elasticsearch filesystem snapshot and waits for completion;
3. stops the stack for an application-consistent volume boundary;
4. archives the eight fixed durable volumes into a timestamped directory;
5. writes a non-sensitive manifest and restarts the stack.

Copy backup directories to encrypted storage with independent retention and
access controls. A backup is not accepted until restore has been exercised on a
separate host or isolated Docker engine.

## Restore verification

Restore overwrites only the eight allowlisted `fa-poc-*` volumes and requires an
explicit switch:

```powershell
pwsh -NoProfile -NonInteractive -File infra/windows-poc/scripts/restore.ps1 `
  -BackupDirectory D:\FinancialAssistantBackups\20260824T120000Z `
  -ConfirmRestore

pwsh -NoProfile -NonInteractive -File infra/windows-poc/scripts/verify.ps1
```

Run restore on an isolated host first. Confirm gateway health, Elasticsearch
snapshot inventory, RabbitMQ queues, MinIO objects, and representative synthetic
user flows. Do not use real identities, financial records, receipts, OCR text,
or prompts as smoke-test data.

## Current PoC limitations

This ticket provides a repeatable deployment and durability substrate. Some
service hosts still use explicit in-memory PoC adapters because their durable
PostgreSQL or object-storage adapters are separate backlog work. PostgreSQL is
provisioned for that migration but does not silently become financial source of
truth. The stack is not ready for first-user testing until the remaining P8/P9
security, monitoring, backup-restore exercise, mobile distribution, and go/no-go
gates are verified.
