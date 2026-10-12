# Native Windows Service Hosting

Jira: FIN-273, child of FIN-270. This module supplies Windows Service lifetime
and a process hosting profile, not an installer or installed-host acceptance.

Owner-approved phase split, 2026-10-10: FIN-273 closes after reviewed hosting code
and free verification; actual SCM registration, recovery and three-Windows service
evidence stay mandatory in FIN-278/281 and owner-run final acceptance. No installed
check is waived or reported passed, and no machine installation is authorized here.

## Runtime Contract

All fifteen production entry points call `FinancialAssistantHost.CreateBuilder`.
`Microsoft.Extensions.Hosting.WindowsServices` 8.0.1 selects the Windows Service
lifetime only when SCM launched the process. Ordinary developer/test execution
keeps console lifetime. SCM requires `--environment WindowsNative`; native and
SCM content roots are the published executable directory, never System32.

The native profile owns the canonical IPv4 loopback ports in
[`NativeServiceCatalog`](FinancialAssistant.Shared.Hosting/NativeServiceCatalog.cs).
Explicit conflicting URLs and arbitrary Kestrel endpoint sections fail before
startup. FIN-207 adds the reviewed HTTPS perimeter; this profile exposes nothing
to remote clients. Native startup requires explicit stable secrets, named in the
catalog: 32-256 printable, non-whitespace ASCII characters. Missing/invalid values
fail with configuration key names only. No secrets are generated or logged.
FIN-208 supplies protected configuration/secret references and per-service ACLs;
environment input here is a bootstrap contract, not that protection mechanism.

The catalog also requires each activated Income/Expense PostgreSQL Password and
IndexKey alongside its gateway secret. These are separate owner storage inputs,
not reusable gateway credentials; persistence validates the retained encryption
key set separately.

Identity and Gateway must receive the same signing material, issuer and audience.
The existing Identity SHA-512 key derivation and HS256 Gateway validation remain
authoritative. All gateway-authenticated downstream services receive the same
gateway hop secret until FIN-208 provisions the supported trust configuration.
Receipt event sender/receiver secrets must match; MCP Monitoring/Audit secrets
must match those services' internal authentication secrets. Never put real values
in arguments, this document, tracked JSON or operator logs.

Native Gateway activates the eighteen existing routes, forces JWT enforcement
and authenticated/admin policies, permits only the seven known public POST auth
endpoints, and pins thirteen destinations to twelve loopback processes. Audit is
5113, Income 5111; Recommendations and Notifications both use 5108. No public MCP
or AI route is added. Native MCP uses HTTP Monitoring/Audit, with Audit 5113;
receipt events target Intake 5104. Profile and AI require a trusted hop secret for
all non-health requests; forged user headers alone confer no trust. Existing
service-specific authorization remains in place.

Native Monitoring replaces the legacy target array with the fourteen other hosts
from the same catalog, including Gateway and Audit, excluding itself and the
nonexistent Financial Summary API. Audit is reported at 5113 and AI at 5114;
RabbitMQ management and Elasticsearch probes are pinned to IPv4 loopback.
Extra or ambiguous array indices fail rather than retaining foreign probe targets.

Native Gateway readiness probes unique downstream `/health/ready` endpoints,
with two-second timeouts, no redirects and no private-body inspection. A missing
or unhealthy process yields 503. SCM dependency running-state is not application
readiness; future installer orchestration must wait for these checks. The process
checks are NOT proof of durable adapters, paid-provider success or installed POC
acceptance. Storage/events/file/observability implementation remains FIN-274/275/
276/277. Real paid AI, OCR and phone activation is rejected in this profile.

Shutdown is bounded to thirty seconds; hosted workers stop through .NET lifetime
cancellation. Closing WPF is unrelated to backend lifetime. A failing worker uses
StopHost; FIN-278 must verify failure exit status and recovery under actual SCM.
JSON console logging stays the existing default; observability clears the automatic
EventLog provider, avoiding implicit EventLog-source registration. FIN-277 owns
service-account-safe durable logging. No privileged mutation happens on startup.

## Candidate Publish

From repository root, with .NET 8 SDK and PowerShell 7:

```powershell
pwsh -NoProfile -NonInteractive -File tools/scripts/publish-native-hosts.ps1 -CandidateId local-check -ServiceId gateway
```

Omit `-ServiceId` to publish all fifteen. The script validates the inventory and
publishes framework-dependent win-x64 executables under the ignored
`.codex-runtime/native-hosts/<candidate-id>/`. It refuses existing output, records
source revision/dirty state and executable hashes, and explicitly sets
`releaseReady=false`, `installed=false`. SDK first-run certificate generation and
telemetry are disabled. No services, accounts, ACLs, firewall rules, certificates,
paid providers or external deployment are created. A failed partial candidate is
not a release; choose a new ID after fixing the failure. Output is not committed.
This is not FIN-280's qualified signed release manifest or FIN-278's installation.

## Installer Handoff And Verification

[`NativeServicePlan`](FinancialAssistant.Shared.Hosting/NativeServicePlan.cs) is
a pure plan, not an SCM actuator. It specifies a canonical service name,
`NT SERVICE\FinancialAssistant.<id>` virtual account, owned versioned executable
path, native environment, delayed automatic start, recovery at 30/60 seconds,
24-hour recovery reset, thirty-second graceful stop, and data-preserving uninstall.
It rejects unknown IDs, path/argument traversal, mismatched product executables,
duplicate registrations and existing registrations with foreign owner/path.

FIN-278 must read actual SCM and protected installation ownership records fresh,
verify all files/trust/ACLs and reparse-point boundaries, quote the executable path
with its spaces, establish dependency order from the qualified component lock,
configure Log on as a service and required ACLs through explicit consent, apply
recovery (no further automatic restarts after the two planned attempts), verify
SCM/runtime state, and remove only installation-owned registrations. Caller-supplied
ownership strings and the unqualified inventory alone are not authorization.
Uninstall must retain ProgramData financial/configuration data unless a separately
approved deletion operation is performed. No such operation is in this module.

Free tests cover manifest/catalog parity, entry-point adoption, missing/unsafe
secrets, external/conflicting endpoint configuration, provider activation, forged
Profile/AI trust, console lifetime and graceful stop, route/trust catalogs,
downstream readiness, service-plan path/ownership conflicts and retained data.
Console/TestServer tests are NOT an actual SCM service-process smoke. Actual
registered-service startup, port collision, recovery, reboot, UI independence,
least-privilege access and cleanup on Windows 11/Server 2022/2025 remain pending
owner-authorized host evidence (FIN-278/281), never passed by static/unit checks.

Primary platform reference:
[Microsoft Windows Service hosting](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/windows-service?view=aspnetcore-8.0).
