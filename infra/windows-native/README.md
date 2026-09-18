# Native Windows Component Inventory

FIN-272, metadata updated 2026-09-18. **Design inventory, not a release lock or an
installer. FIN-272 remains In Progress.** The approved target is the
[Windows-native POC](../../docs/architecture/windows-native-poc.md), without
Docker, WSL or a Linux VM. No machine configuration is changed here.

## Read-Only Verification

[component-manifest.json](component-manifest.json) inventories all 15 production
ASP.NET hosts, the admin web assets, future WPF/engine assets, twelve native
package candidates and required native capabilities. It distinguishes upstream
metadata from verified local payloads and actual installed-host acceptance.

From the repository root, with existing PowerShell 7:

```powershell
pwsh -NoProfile -NonInteractive -File tools/scripts/test-native-component-manifest.ps1
```

This performs offline structural checks, exact source-host coverage, unique
ports/IDs, dependency references/cycles, digest shape and safety-policy checks.
It requires explicit prerelease denial and retains every named capability and
qualification blocker; deleting backup, secret recovery or observability cannot
silently produce a valid inventory. Duplicate safety entries are also rejected.
The admin web assets, WPF wizard and installation engine are mandatory entries.
Each asset must declare exactly `not-implemented`, `not-tested` or `blocked`;
missing, non-string or release-ready qualification values are rejected.
It reads files only. It does not download, install, run probes, verify binary
signatures, certify licenses, query vendor support or approve a release.
`schemaValid: true` always accompanies `releaseReady: false`. A mutated inventory
claiming release readiness is rejected. This is not an input to an execution
engine; FIN-278 must use a separately specified signed, fully qualified lock.

## Hosts And Ports

These are proposed installer assignments, not current working configuration.
Every internal listener binds to `127.0.0.1`; only the separately configured HTTPS
gateway endpoint may be exposed. Local binding never replaces authentication.
SCM lifetime, service identities and final gateway destinations belong to FIN-273.

| Host | Proposed port | Native state/adapter owner |
| --- | --- | --- |
| Public gateway | 5100 | FIN-273, no financial authority |
| Identity | 5101 | FIN-274 |
| Profile | 5102 | FIN-274 |
| Category | 5103 | FIN-274 |
| Transaction intake | 5104 | FIN-274 |
| Receipt processing | 5105 | FIN-277 |
| Analytics | 5106 | FIN-275 |
| Financial score | 5107 | FIN-275 |
| Recommendations and notifications, one existing host | 5108 | FIN-275 |
| Monitoring | 5110 | FIN-277 |
| Income | 5111 | FIN-275 |
| Expense | 5112 | FIN-275 |
| Audit | 5113 | FIN-277 |
| AI orchestration | 5114 | FIN-274 |
| MCP, never a public gateway route | 5115 | FIN-273 |

Existing gateway configuration sends both Income and Audit to port 5111, leaves
many destinations disabled and models recommendations/notifications as separate
destinations although one host implements them. The manifest deliberately does
not overwrite those settings. FIN-273 must reconcile them and test real routing.
All hosts expose the shared `/health/live` and `/health/ready` contract; readiness
must gain actual dependency/schema checks before it can prove native operation.
The graph describes prerequisite startup/readiness ordering, not financial data
ownership or authorization. Required worker loops run in their owning hosts;
new separately hosted workers must be added to the inventory when implemented.

The admin UI is prebuilt static content from `web-admin/monitoring-ui`, served
behind protected HTTPS. No Node, npm, Git or SDK is installed on tester machines.
Consumers on mobile and web do not become Windows-only.

## Package Candidates

Exact hashes and origins already retrieved are in the JSON, never invented
placeholders. `null` integrity means qualification is blocked. A provider's
published hash is not evidence that downloaded bytes or a signature were checked.

| Candidate | Evidence and unresolved qualification |
| --- | --- |
| .NET Runtime, ASP.NET Core Runtime and Desktop Runtime 8.0.31 x64 | Official SHA-512 metadata captured; signature chain/revocation and binary notices pending |
| PostgreSQL 18.6 x64 EDB distribution | Exact distribution revision, payload URL/hash, prerequisite inventory and bundled terms pending |
| Erlang/OTP 27.3.4.17 x64 | Official release asset SHA-256 captured; binary trust and unattended lifecycle not tested |
| RabbitMQ 4.3.5 | Official release asset SHA-256 and detached-signature location captured; signing-key trust and native lifecycle not tested |
| NSIS 3.12 build-time packaging candidate | Native compiled bootstrap plus WPF and separate elevated engine; compiler digest/notices and product signing identity pending |
| Elasticsearch 8.19.21; Prometheus 3.14.0; Alertmanager 0.34.1; Jaeger and tools 2.21.0; Grafana OSS 13.2.2 | Exact Windows artifact URLs and published hashes captured; service hosting, support and binary qualification remain blocked |

See [search and observability qualification](search-observability.md) for the
candidate signal paths, additional ports, service-wrapper gap and per-OS evidence.

Microsoft's [runtime composition and installer options](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
distinguish the base runtime from ASP.NET and Desktop. ASP.NET alone does not
provide the base runtime. Desktop also includes it; detection must not install a
duplicate unnecessarily. The native bootstrap must obtain explicit prerequisite
consent before provisioning Desktop Runtime and launching framework-dependent WPF.
The WPF UI cannot be assumed to launch on a clean machine before that dependency.
No automatic .NET major migration or SDK install is performed.

The [support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
lists .NET 8 end of support as 2026-11-10. The current candidate is constrained to
release and supported operation before that boundary, with current patches.
No supported deployment extending past it may ship without separately delivered
runtime migration work. This date is a restriction, not a delivery ETA or a claim
that the current package remains the newest patch at future packaging time.

[PostgreSQL's Windows page](https://www.postgresql.org/download/windows/) lists
Server 2022/2025 for version 18; desktop comparability is not a Windows 11 test
certificate. All three product targets still need our acceptance evidence.
The [EDB archive listing](https://www.enterprisedb.com/download-postgresql-binaries)
is a discovery URL, not a pinned downloadable payload. The
[PostgreSQL license](https://www.postgresql.org/about/licence/) does not by itself
complete the license inventory of an entire vendor archive.

[RabbitMQ compatibility](https://www.rabbitmq.com/docs/which-erlang) supports the
selected 27.x Erlang series; newer OTP major versions must not be substituted
automatically. Its [release policy](https://www.rabbitmq.com/release-information)
lists community support for 4.3 through 2026-11-30. Commercial support is not
assumed. [Windows installation guidance](https://www.rabbitmq.com/docs/install-windows)
requires x64 Erlang, careful service/cookie configuration and compatible paths.
An existing incompatible Erlang installation blocks unattended takeover.

[NSIS 3.12](https://nsis.sourceforge.io/Download) is a candidate, not installed or
approved here. Review its [module-specific licenses](https://nsis.sourceforge.io/License)
and all included plugins; use no optional third-party plugin without qualification.
WiX/Burn is an alternative, but its current
[maintenance-fee terms](https://docs.firegiant.com/wix/) require an applicability
decision before use. Do not assume paid eligibility or select an unsupported old
WiX release to avoid that decision. WPF remains the operator UI whichever engine
is qualified; no hand-written MSI transaction engine is proposed.

## Lifecycle And Resources

Candidate silent arguments are data, never executable instructions. Microsoft
`/install /quiet /norestart` and NSIS-family `/S` need exact-payload validation.
Do not infer that all vendor executables share exit/reboot codes. Record each
package's install/detect/repair/remove behavior and accepted exit codes in the
qualified lock. Unknown code, timeout or unexpected reboot request is a failure;
reconcile observed state before retry. Never pass database passwords or cookies
on command lines, through public environment dumps, or into support logs.

PostgreSQL archive provisioning will need owned data initialization, restricted
service identity, per-service database roles, migrations and restart probes.
Its exact Visual C++/other native dependencies must be inventoried before approval.
Do not copy bundled executables and assume their runtime dependencies are present.

Reserve PostgreSQL 5432, Erlang 4369, RabbitMQ 5672/25672 and optional protected
management 15672 locally. RabbitMQ CLI uses additional dynamic client ports;
validate the bounded vendor range during installation rather than opening them
publicly. Extra plugins are off unless specifically required. A readiness probe
must authenticate and check publish/confirm/consume, not only process existence.

Resource sizing remains **unmeasured**. Do not present guessed CPU/RAM/disk figures
as minimum requirements. FIN-272 qualification must record selected payload sizes,
expanded footprint, current+new binary staging, database/receipt growth, retained
logs/indices, backup headroom and temporary extraction allowance. FIN-281/206 must
measure representative synthetic workloads and enforce tested preflight thresholds.
Unreadable disk capacity or unknown required footprint blocks installation.

Application binaries and data are separate. Each service gets its own durable
schema/role and protected data root. No service shares authoritative tables.
Repair only owns declared assets; uninstall retains data/backups and never removes
shared runtimes, existing PostgreSQL/Erlang or unrelated Windows services.
Schema-incompatible upgrades require a tested restore plan, not binary rollback.

## Capability Decisions

Native encrypted receipt files plus durable PostgreSQL metadata replace the
container object-store dependency, subject to FIN-277 key recovery/path/ACL tests.
In-process caches may hold disposable values only: sessions, idempotency, provider
quotas and financial state require durable owner stores. No unsupported Redis
Windows port is selected. Durable event transport is RabbitMQ with atomic outbox,
inbox, retries and restart convergence, not current in-memory publishers.

Elasticsearch is restricted to retained read/search/operational contracts; the
old 8.15.3 container pin is not a native package approval. The
[Windows service distribution](https://www.elastic.co/docs/deploy-manage/deploy/self-managed/install-elasticsearch-with-zip-on-windows)
and [vendor support matrix](https://www.elastic.co/support/matrix) must be reconciled
with exact payload/version, bundled JDK, licensing and all three OS targets.
Native metrics, traces, persistent audit, dashboards and alert delivery also need
explicit packages/adapters. Local log files alone do not satisfy these contracts.
Until this is resolved, `SEARCH-OBSERVABILITY` blocks package qualification.

FIN-207 owns the proposed Kestrel HTTPS endpoint and trusted certificates;
FIN-208 owns secret protection and key recovery; FIN-209 owns consistent restore.
LLM/OCR/push/SMS/OAuth integration remains separately authorized and disabled when
unconfigured. A disabled provider is unavailable, never a successful test result.

## Remaining FIN-272 Work

Owner clarification, 2026-09-18: paid operations are excluded from POC development
and testing. Final installed-POC testing is owner-run after development. Continue
the manifest, implementation and free verification; prepare reproducible scripts
and explicit pending checks for handoff. Do not buy a signing certificate, activate
a paid provider or provision paid test hosts to unblock this inventory. Metadata,
integrity and security requirements still apply; untested is not approved.

1. Pin the exact PostgreSQL archive/revision and all required native dependencies.
2. Qualify the pinned search/observability candidates, service wrapper and support/license matrix.
3. Verify downloaded payload hashes, trusted signatures, notices and redistribution
   terms; capture exact unattended commands, exit codes and resource requirements.
4. Resolve bootstrap/compiler qualification and the approved product-signing path.
5. Publish the qualified matrix through CI/review, update Confluence and only then
   evaluate FIN-272 completion. Runtime implementation and three-host acceptance
   remain later gates, even after package metadata is complete.

No additional spending, package/tool installation or machine changes are authorized
by this document. Current POC completion remains 183/207 (88.4%); first-user testing
is Not Ready. See [the progress ledger](../../docs/agent/POC_PROGRESS.md).
