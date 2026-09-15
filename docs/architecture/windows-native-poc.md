# Windows-Native POC Deployment

Decision date: 2026-09-15. User-approved change. Story: FIN-270; baseline: FIN-271.
Status: **Approved target, not implemented or runtime accepted**.

## Decision

The POC backend must install directly on **Windows 11**, **Windows Server 2022**
and **Windows Server 2025**, initially x64. Server targets require **Desktop
Experience** for the WPF wizard. Server Core, ARM64 and older Windows versions
are outside the initial acceptance matrix, not silently supported.

There is **no Docker, WSL or Linux VM requirement**. A WPF installer provisions
the complete required server stack as native Windows services/components. WPF
is an operator installation and maintenance interface, not a consumer desktop
client. Android/iOS/Web product scope, REST contracts, business service ownership
and deterministic financial authority remain unchanged.

This supersedes the Compose deployment criterion of FIN-42/206 and the old
Compose-specific FIN-204 blocker. Existing Compose assets, tests and completed
evidence remain historical/developer compatibility material. They must not be
deleted just to make this decision appear implemented. Native prerequisites,
adapters and installed-host acceptance are still missing work; a changed ticket
description is not a passed test.

## Runtime And Installation Boundaries

```text
Operator -> WPF wizard -> authenticated elevated installation engine
                              |
                       versioned package manifest
                              |
                  Windows Service Control Manager
                              |
Client -> HTTPS perimeter -> Public API Gateway -> owned REST APIs/workers
                                                  |           |
                                         PostgreSQL       RabbitMQ
                                         owned state    outbox -> inbox
                                                  |           |
                                      encrypted files   derived insights
                              |
                    protected health/admin diagnostics
```

The WPF UI is not a service supervisor. Closing it does not stop the server.
The engine has a reusable unattended entry point for verified automation, while
the interactive wizard remains the normal operator experience. Elevation is
limited to explicit machine changes; running backend services use dedicated
least-privilege identities. UI-to-elevated-worker IPC must authenticate the caller
and accept a validated plan, never arbitrary command strings or executable paths.

Only the selected HTTPS entry point is exposed. Internal HTTP endpoints and
database/broker/admin ports bind to loopback or another explicitly approved
restricted boundary. MCP is not a public route. Service-to-service authentication
and user authorization remain mandatory even on the same host.

## Native Component Plan

FIN-272 must inventory every required component, select compatible supported
versions and prove unattended installation, health, upgrade and redistribution
rights. This table is a target, not an approved binary bill of materials.

| Component | Native target | Owner and release gate |
| --- | --- | --- |
| WPF wizard and engine | Separate UI and privileged deployment engine | FIN-278/279; resumable steps and UI validation |
| .NET APIs/workers | Versioned win-x64 applications with Windows Service lifetime | FIN-273; all required hosts, real gateway routes and trust configuration |
| Identity/Profile/Category/Intake | Service-owned durable state, migrations and idempotency | FIN-274; restart and owner-isolation evidence |
| Financial/insight services | PostgreSQL authoritative records and owned derived state | FIN-275; deterministic decimals/currencies, atomic owner commit/outbox, rebuild |
| RabbitMQ and Erlang | Compatible native Windows packages and service | FIN-276; real event convergence, deduplication, retry and recovery |
| Receipts and file metadata | Encrypted service-owned native file storage with durable metadata | FIN-277; ACLs, safe paths, retention and portable recovery |
| Cache | Bounded disposable native/in-process adapter where semantics allow | FIN-277; no financial/session/idempotency authority in volatile cache |
| Search/operational indices | Native Elasticsearch only where retained contracts require it | FIN-272/205; not an Elasticsearch-first financial database |
| Monitoring/Audit/MCP/admin | Required services plus supported native collectors or protected local sinks | FIN-277/216; no silent loss of required metrics, audit or diagnostics |
| HTTPS and configuration | Supported Windows-native perimeter and protected configuration | FIN-207/208; certificate/key ACLs, rotation, firewall and trust tests |

Do not wrap the old Linux images in hidden virtualization. Redis, MinIO,
Prometheus/Grafana and NGINX from the old stack are not automatically mandatory
native installer dependencies. Each required capability must either have an
approved native implementation or remain Blocked. No unmaintained Windows port,
paid substitute or silent loss of functionality may be used to clear the gate.

Applications remain C#/.NET 8 until a separately tested compatibility change.
The package decision must review runtime support before release: .NET 8 support
ends on 2026-11-10 according to Microsoft's lifecycle notice. Bundling a runtime
does not extend support. FIN-272 must either constrain the release/support window
or create the necessary supported-runtime migration work; no SDK upgrade is
performed by this architecture change.

## Package And State Layout

- Immutable versioned application files under a protected Program Files location.
- Service-owned data, non-secret configuration and redacted installation journal
  under an operator-selectable protected ProgramData/data root, outside binaries.
- Per-service database roles/schemas, native service identities and file ACLs.
- Secret values in an approved Windows-protected store or certificate reference;
  never plaintext release `.env`, CLI arguments, logs, screenshots or support ZIPs.
- Machine-bound encryption requires a separately designed portable key recovery
  path. Copying encrypted bytes to another host is not a restore strategy.
- Verified offline bundle or explicitly consented allowlisted downloads, with
  hashes, publisher trust, versions, SBOM and redistribution/license notices.
- No SDK, Git checkout, manual Compose command or package manager required for
  the end user. Packaging engine selection must prefer a supported existing
  Windows installation mechanism; do not hand-roll an MSI transaction engine.

The manifest defines each component's source, architecture, version, integrity,
license, prerequisites, silent install/repair/remove behavior, restart codes,
ownership, health probe, data locations and rollback compatibility. Existing
shared installations are detected and never silently overwritten or removed.

## Full Operator Flow

1. Launch the versioned installer and verify publisher/package identity.
2. Select new installation, update or repair; detect existing owned state.
3. Check OS/architecture, elevation, disk/RAM, ports, pending reboot, native
   dependencies and the selected package's integrity and support window.
4. Choose application/data locations and local endpoint/certificate settings.
   Validate paths, ACL feasibility and port conflicts before mutation.
5. Provision service configuration and first administrator securely. External
   AI/OCR/notification providers stay disabled unless separately authorized;
   missing credentials show unavailable capability, never fake success.
6. Show an exact change plan, dependency/license requirements, data retention
   policy and any reboot. The operator explicitly starts machine changes.
7. Acquire an installation lock; journal non-secret checkpoints; install native
   dependencies, migrate owned stores, register services, configure trust/TLS,
   and start services in dependency order.
8. Poll bounded readiness and run synthetic authentication/profile/draft/confirm/
   analytics/score/recommendation checks through actual APIs and event transport.
   A running process, health-only result or direct projector call cannot pass E2E.
9. Show verified results, endpoint, installed versions and safe diagnostic report.
   Failure leaves an explicit recoverable state with retry/resume or compensation.
10. Later use the same package/engine for update, repair, diagnostic export or
    uninstall. Backend operation never depends on an open wizard window.

## Failure And Lifecycle Contract

One machine installation lock protects concurrent changes. Steps are idempotent
with durable checkpoints and bounded timeouts. Cancellation occurs at safe
boundaries; interrupted/rebooted runs reconcile observed state before resuming.
Secret input is not persisted into the journal. Package tampering, unknown
publisher, missing dependencies or unsupported OS fails before affected mutation.

Updates require compatible schema/version checks and verified pre-update backup.
An irreversible migration cannot be undone by swapping old binaries: stop and
require a reviewed restore/recovery path. Repair reconciles only owned assets.
Uninstall **retains data and backups by default**, removes only owned services
and files, and never removes a shared third-party dependency. Data destruction
requires separate explicit scoped confirmation and approved recovery preparation.

FIN-209 owns consistent PostgreSQL/receipt/configuration/key recovery, retained
search snapshots or projection rebuild and RabbitMQ/outbox/inbox reconciliation.
Archive creation alone is not recovery verification. Scheduled backup and reboot
behavior must use native Windows mechanisms and be tested on the target host.

## Acceptance And Authorization

FIN-281 supplies the lifecycle test harness. FIN-206 independently records actual
installed-host acceptance on all three OS targets; FIN-204 records business E2E.
Test clean install, reboot, failed prerequisite, non-admin rejection, port
collision, bad hash, interrupted run, repeated run, upgrade, repair, rollback
compatibility and data-preserving uninstall. Use only synthetic data and record
installer/source/artifact hashes, OS build, times, CI and per-case results.

FIN-215 security, FIN-216 operational evidence and FIN-218 release-owner Go/No-Go
remain independent, as do mobile/device/store/provider gates. Missing host,
signing identity, durable adapter or evidence is Blocked, not skipped or Passed.
First-user testing remains **Not Ready** until these gates pass.

Planning and repository changes do not authorize machine-level installation,
firewall exposure, destructive restore, SDK/runtime installation, paid builds,
certificate/license purchase or live provider usage. Obtain required separate
authorization before those actions; no additional spending is assumed.

## Delivery Map

| Jira | Deliverable |
| --- | --- |
| FIN-270 | Parent story; close only after children and dependent release gates |
| FIN-271 | This decision, documentation synchronization and backlog reconciliation |
| FIN-272 | Native component/support/package manifest decision |
| FIN-273 | Native service hosting, gateway routes and trust |
| FIN-274 | Durable identity/profile/workflow state |
| FIN-275 | Durable financial/insight state |
| FIN-276 | Durable RabbitMQ delivery and convergence |
| FIN-277 | Native receipt/cache/diagnostic adapters |
| FIN-278 | Restart-safe privileged installation engine |
| FIN-279 | WPF installation/maintenance wizard |
| FIN-280 | Versioned packages, update/repair/uninstall |
| FIN-281 | Windows lifecycle automation/harness |
| FIN-204/205/206 | Actual business E2E, retained search validation, installed-host acceptance |
| FIN-207/208/209 | Native HTTPS, secrets and verified recovery |
| FIN-215/216/218 | Security, operations and final Go/No-Go |

Execute by dependency-aware Jira rank, not by key number. Existing open tasks
were revised instead of duplicated. Completed tasks and old denominator/history
are preserved. Adding eleven canonical leaves expands scope; the resulting
percentage drop is not lost delivered work. Recompute the
[POC ledger](../agent/POC_PROGRESS.md) from live Jira after every closure.

## Source Verification

Official sources reviewed 2026-09-15; exact package/support decisions remain
FIN-272 work and must be rechecked at packaging time:

- [Microsoft Windows Service hosting](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service)
- [PostgreSQL Windows packages](https://www.postgresql.org/download/windows/)
- [RabbitMQ Windows installation and Erlang prerequisite](https://www.rabbitmq.com/docs/install-windows)
- [Elasticsearch Windows ZIP/service installation](https://www.elastic.co/docs/deploy-manage/deploy/self-managed/install-elasticsearch-with-zip-on-windows)
- [Redis supported installation paths](https://redis.io/docs/latest/operate/oss_and_stack/install/)
- [.NET 8/9 end-of-support notice](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/)

See [storage policy](storage-policy.md), [current implementation](current-implementation.md)
and the [superseded Compose deployment record](../delivery/windows-server-poc-deployment.md).
