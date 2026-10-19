# Native Windows Component Inventory

FIN-272, metadata updated 2026-10-09. **Design inventory, not a release lock or an
installer. FIN-272 inventory scope is merged through PR270 and verified Done;
final qualification remains a release gate.** The approved target is the
[Windows-native POC](../../docs/architecture/windows-native-poc.md), without
Docker, WSL or a Linux VM. No machine configuration is changed here.

FIN-273's [hosting contract](../../backend/shared/hosting/README.md) documents the
native process profile, canonical service ports, required trust and non-executing
SCM plan. Its publish script creates explicitly unqualified win-x64 candidates,
not an installer or qualified release; the inventory remains `releaseReady=false`.

FIN-277 implements [native receipt/cache/diagnostic adapters](../../docs/engineering/windows-native-adapters.md).
Owned NTFS object files, ACL/path/key/retention controls, typed disposable health
cache and a protected technical sink have free synthetic test/owned-crash fixtures.
FIN-285 adds [owned Receipt/OCR and AI workflow metadata](../../docs/engineering/receipt-ai-native-persistence.md);
delivery readiness remains blocked pending FIN-276. No native
collector, Elasticsearch, WPF/SCM, backup-system or three-Windows acceptance is
granted; package/target qualification and the release-ready flag remain unchanged.
PR285 actual merge `dfe9692f1da22aa22e864d4eb9932d5e67316af0` at
`2026-10-15T18:24:45Z` is independently verified; FIN-277 Done, final80ee33b all5CIgreen.
Fresh census18:26:46 UTC:191/215 (88.8%), +0.5 pp before rounding; FIN-2709/19,
P9 15/39 (38.5%). That FIN-277 snapshot is historical.
FIN-285 is Done after actual PR287 merge `8a3d53f9508de9eb1577ff377dbe49c73403e4a5`
at `2026-10-19T20:26:56Z`, final297354d/all5CIgreen/zero-total review fixed.
Fresh2026-10-19 20:28:57 UTC census:192/215(89.3%),+0.5pp before rounding;
FIN-27010/19, P9 16/39(41.0%). First-user Not Ready. FIN-286 remains blocked by
unfinished delivery/derived-owner persistence; follow prerequisites only after
this closure ledger actually merges and fresh dependency reads.

FIN-274's [persistence foundation](../../backend/shared/persistence/README.md)
is distinct from domain owners FIN-282/283/284/285 and full integration FIN-286.
The manifest's `sourceCommit` retains the original package-inventory evidence;
updated persistence ownership is tracked in the scoped implementation PR, not a
claim of signed artifacts, installed adapters or release qualification.

FIN-282's [native Identity contract](../../docs/engineering/identity-native-persistence.md)
is merged through [PR276](https://github.com/Marachert/FinancialAssistant.Host/pull/276)
as `d8530fb8a6dde87559fc07fc52dd89352e0afd9e` at `2026-10-11T12:14:21Z` and Jira Done.
Final head `e12c388f842942fa8951c53c0e2ca2370e0df559` passed all five checks
(Backend38137763784, Mobile38137763793, Admin38137763841); both valid review findings
were fixed, evidenced and resolved only after green. Actual synthetic PostgreSQL
initial/crash/restart, atomic state/outbox, owner isolation, provider-lock separation,
hard-retention bounds and owned cleanup passed; no registered service or installed
matrix is claimed. Native broker readiness remains pending FIN-276. Profile/Category
FIN-283 [native Profile/Category storage](../../docs/engineering/profile-category-native-persistence.md)
is merged through [PR278](https://github.com/Marachert/FinancialAssistant.Host/pull/278)
as `dba7624e84bcfac7991fc41610e8ad829716e0e6` at `2026-10-11T18:01:35Z` and Jira Done.
Final head `246ec784ea061a468ad51b8906ca2604da591b1f` passed all five checks
(Backend38161517454, Mobile38161517466, Admin38161517445); both input-validation
findings were fixed/regressed, evidenced and resolved only after green. Exact
preferences/catalog rehydration, owner-isolated concurrency, atomic Category/outbox,
deletion/replay and real crash/restart passed with owned cleanup; local baseline
plus review reruns 955/955 in 24 projects, zero skips. Minimal replay markers and
pending metadata remain retained until qualified lifecycle horizons, not full
cross-service privacy erasure. At the FIN-283 closure checkpoint, FIN-284 was next by rank;
remaining domain/event/object/installer
and three-Windows gates remain required. Historical [closure ledger](../../docs/agent/POC_PROGRESS.md)
is 188/212 (88.7%), +0.5 pp; FIN-270 6/16 and FIN-38 P9 12/36 (33.3%), first-user Not Ready.

The subsequent [FIN-275 ownership inventory](../../docs/engineering/native-financial-persistence-scope.md)
retains all seven original financial/read-model/audit owners in four sibling
leaves: FIN-275 Income/Expense, FIN-287 Summary/Analytics, FIN-288 Score/
Recommendations/Notifications, and FIN-289 Audit. This is planning, not additional
implemented adapters. The fresh 2026-10-11 18:42:32 UTC census keeps 188 Done and
adds three unfinished leaf units: historical scope188/215 (87.4%), a -1.2 pp
granularity adjustment, not lost delivery. At that checkpoint FIN-270 had 6/19
children Done and P9 was 12/39 (30.8%).
The [FIN-275 financial adapter implementation](../../docs/engineering/financial-records-native-persistence.md)
adds separate Income/Expense native stores and atomic owned outboxes, not live
broker delivery or installer qualification. Public reconciliation reads remain
owner-authenticated; Intake confirmation remains event-only. PR281 was independently
verified merged as `fae3226412e05bef6a6a398f30fec0327889173b` at
`2026-10-12T08:14:15Z` and FIN-275 is Done. Final head
`7907369d967a89be78c7663cd3d06c414f985b3a` passed all five checks
(Backend38221608361/Mobile38221608355/Admin38221608347); the catalog-secret
finding was fixed/regressed/replied/resolved only green. Latest per-project
966/966 tests across24projects, zero failures/skips; real isolated PostgreSQL
initial/crash/restart, atomic rollback, corrected/conflicting/post-retention replay
and pending-safe bounded retention passed with owned cleanup. Native readiness
still waits for FIN-276, not fake delivery or installed qualification.

At the FIN-275 checkpoint, FIN-284 was next by local rank with FIN-274/275 Done
prerequisites. Historical census
2026-10-12 13:26:08 UTC:189/215 (87.9%), +0.5 pp before rounding(0.4651);
FIN-2707/19 and FIN-38 P9 13/39 (33.3%). First-user testing remains Not Ready;
no installed feature credit or calendar ETA.

FIN-284 [native Intake storage](../../docs/engineering/intake-native-persistence.md)
adds durable draft/index/source/confirmation-intent outboxes, not live broker or
installer acceptance. Intake requires explicit source retention and its own
storage secrets; no in-process native financial stores/consumers or write RPC.
PR283 was independently verified merged as
`48c7c0941b4b58cf3f7884c5c16182a9803f6594` at `2026-10-15T15:35:33Z` and
FIN-284 is Done. Final head `4eb97460da5aa39cb7bb94dc7c54f97aa74dd8c3`
passed all five checks (Backend38267364535/Mobile38267364389/Admin38267364375).
The OCR identifier finding was fixed with nine native HTTP/pre-SQL regressions,
replied and resolved only after green. Initial full975/24, final scoped54/54 and
actual owned PostgreSQL crash/restart evidence remain separate from live broker
and installed acceptance. Fresh census2026-10-15 16:13:51 UTC:190/215 (88.4%),
+0.5 pp before rounding; FIN-2708/19 and FIN-38 P9 14/39 (35.9%).
At the FIN-284 checkpoint FIN-285 was next but blocked by FIN-277; follow that prerequisite
only after the closure ledger is actually merged and dependencies are read fresh.
First-user testing remains Not Ready, with no calendar ETA inferred.

Financial Summary currently has no standalone API host in the fifteen-host
catalog. FIN-287 must explicitly declare its module-owned storage identity and
provisioning without silently sharing another owner's role or inventing a new
deployable/port. FIN-277 still owns diagnostic collectors/sinks and manifest
qualification; FIN-289's Audit metadata storage does not remove that capability.

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
All twelve named native packages are also mandatory, even when removing a whole
diagnostics topology leaves no dangling dependency references. Every single
package removal, combined observability removal and an empty package set are
covered by regression tests; design validation never means release qualification.
Each asset must declare exactly `not-implemented`, `not-tested` or `blocked`;
missing, non-string or release-ready qualification values are rejected.
It reads files only. It does not download, install, run probes, verify binary
signatures, certify licenses, query vendor support or approve a release.
`schemaValid: true` always accompanies `releaseReady: false`. A mutated inventory
claiming release readiness is rejected. This is not an input to an execution
engine; FIN-278 must use a separately specified signed, fully qualified lock.

For an operator-supplied payload, the separate offline inspector verifies its
publisher digest before reading ZIP metadata:

```powershell
pwsh -NoProfile -NonInteractive -File tools/scripts/inspect-native-package.ps1 -PackageId postgresql -PayloadPath <local-archive>
```

It does not download, extract or execute files. ZIP paths must be safe and unique
on Windows; symbolic links and configured entry/expanded-byte limits are rejected.
Directory entries must declare zero uncompressed bytes; data-bearing directories
are rejected rather than silently omitted from the expanded-byte accounting.
Every path component is limited to 255 UTF-16 code units, including directory
names and surrogate pairs. Total deployment path/volume/long-path policy remains
an installation-engine check; this is not permission to materialize arbitrary paths.
Before .NET loads ZIP entries, bounded EOCD/ZIP64 headers and a streamed directory
scan check declared and actual counts, byte bounds and offsets. Defaults are
100000 entries, 64 MiB central-directory bytes and 12 GiB declared expanded bytes.
Multi-disk and nonstandard directory records are rejected. These limits bound
inspection, not installation sizing. Reserved Windows device names include the
superscript digits described by [Microsoft's naming rules](https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file).
Trailer/directory bounds follow [PKWARE's ZIP specification](https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT).
Declared sizes and notice filenames are evidence to
review, not CRC verification, license clearance or actual installed disk usage.
The report never claims verified binary signatures or release readiness.

## Hosts And Ports

These are canonical WindowsNative hosting assignments, not installed acceptance.
Every internal listener binds to `127.0.0.1`; only the separately configured HTTPS
gateway endpoint may be exposed. Local binding never replaces authentication.
SCM lifetime, service identities and final gateway destinations belong to FIN-273.

| Host | Proposed port | Native state/adapter owner |
| --- | --- | --- |
| Public gateway | 5100 | FIN-273, no financial authority |
| Identity | 5101 | FIN-282, foundation FIN-274 |
| Profile | 5102 | FIN-283, foundation FIN-274 |
| Category | 5103 | FIN-283, foundation FIN-274 |
| Transaction intake | 5104 | FIN-284, financial APIs FIN-275 |
| Receipt processing | 5105 | FIN-285 metadata; FIN-277 encrypted objects |
| Analytics | 5106 | FIN-287 |
| Financial score | 5107 | FIN-288 |
| Recommendations and notifications, one existing host | 5108 | FIN-288 |
| Monitoring | 5110 | FIN-277 |
| Income | 5111 | FIN-275 |
| Expense | 5112 | FIN-275 |
| Audit | 5113 | FIN-289 metadata; FIN-277 diagnostics wiring |
| AI orchestration | 5114 | FIN-285 metadata; paid providers disabled |
| MCP, never a public gateway route | 5115 | FIN-273 |

Historical developer gateway settings sent Income and Audit to port 5111 and
left destinations disabled. Merged FIN-273 code corrects the WindowsNative
profile: Audit5113, Income5111 and both recommendation/notification destinations
on their one 5108 host, with all known routes active. Developer settings remain
unchanged; installed routing/SCM acceptance is still pending.
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
| .NET Runtime, ASP.NET Core Runtime and Desktop Runtime 8.0.31 x64 | Downloaded EXEs match official SHA-512; installer Authenticode Valid for Microsoft; contained files, notices and lifecycle pending |
| PostgreSQL 18.6-5 x64 EDB binary archive | Publisher SHA-256 and exact archive URL/size captured; binary trust, application-control compatibility, prerequisites and bundled terms pending |
| Erlang/OTP 27.3.4.17 x64 | Downloaded EXE matches official SHA-256; installer Authenticode Valid for Ericsson AB; contained files and unattended lifecycle pending |
| RabbitMQ 4.3.5 | Downloaded EXE matches official SHA-256; installer Authenticode Valid for VMware, Inc.; detached-key trust, contained files and native lifecycle pending |
| NSIS 3.13 build-time packaging candidate | Official release and Microsoft WinGet installer digest captured; local compiler trust/notices, bootstrap and product signing identity pending |
| Elasticsearch 8.19.21; Prometheus 3.14.0; Alertmanager 0.34.1; Jaeger and tools 2.21.0; Grafana OSS 13.2.2 | All six downloaded payload digests match; selected PE/signature and SBOM evidence recorded; service hosting, notices, support and complete trust qualification remain blocked |

See [search and observability qualification](search-observability.md) for the
candidate signal paths, additional ports, service-wrapper gap and per-OS evidence.

On 2026-10-09 passive Windows checks of the three .NET EXEs and Erlang/RabbitMQ
EXEs reported Authenticode `Valid`, with the publishers above. Exact sizes,
file versions and certificate thumbprints are retained in the manifest. These
are observations of the installer files on this machine, not a permanent trust
allowlist, proof of all bundled files or installed-host acceptance. Revalidate
expected publisher, chain and revocation at release/install time; never accept
a different payload merely because it uses the same certificate. No EXE was run.

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
The [upstream version policy](https://www.postgresql.org/support/versioning/)
sets major 18 maintenance through 2030-11-14; this does not certify the vendor
Windows archive, confer commercial support or extend the .NET 8 boundary.
On 2026-10-09 the candidate moved from revision 4 to revision 5, using the
[publisher's exact ZIP checksum](https://github.com/EnterpriseDB/edb-installers/issues/733#issuecomment-6012030716).
A read-only HEAD request confirmed the pinned HTTPS URL, HTTP 200 and 384620317
compressed download bytes. The checksum applies only to
`postgresql-18.6-5-windows-x64-binaries.zip`; it does not authenticate revision 4,
an installer EXE or PostgreSQL source archives. The exact archive was then
downloaded into ignored local storage and its SHA-256 matched the publisher
value. Offline inspection found 22098 ZIP entries, 20554 files and 1010746573
declared expanded bytes. No component was executed or installed. Declared size
is not a measured installed footprint, and an ETag is not an integrity check.

The archive includes `pgsql/server_license.txt` and
`pgsql/commandlinetools_3rd_party_licenses.txt`, plus separate pgAdmin and
StackBuilder notices. Server/client `bin`, `lib` and `share` entries declare
147885345 bytes; this candidate subset is not yet an approved deployment allowlist.
Do not ship pgAdmin, StackBuilder, their Python environment or optional extensions
without a product requirement and qualification. Preserve all applicable notices,
identify exact dependency versions and required source/relinking obligations before
redistribution; merely finding a license file does not complete that review.

On 2026-10-09 the three fixed entries `pgsql/bin/postgres.exe`, `psql.exe` and
`libpq.dll` were copied to owned temporary files for read-only signature/version
inspection, then removed. All reported version 18.6 and Authenticode `NotSigned`.
Their SHA-256 values are retained in the manifest. They were never executed or
registered as services; this does not establish the trust of every bundled file.

The [vendor's signing response](https://github.com/EnterpriseDB/edb-installers/issues/713#issuecomment-6040806815)
confirms broader component signing is planned for a future release. Reports in
that issue concern unsigned components blocked by Windows application control.
Do not assume a signed installer makes all bundled EXEs/DLLs trusted. Qualification
remains blocked until the selected binaries work with the target host's protection
policy; never turn off Smart App Control, Defender or WDAC to pass installation.
The publisher digest does not establish Authenticode trust, native dependency
completeness or host acceptance. The
[PostgreSQL license](https://www.postgresql.org/about/licence/) does not by itself
complete the license inventory of an entire vendor archive.

[RabbitMQ compatibility](https://www.rabbitmq.com/docs/which-erlang) supports the
selected 27.x Erlang series; newer OTP major versions must not be substituted
automatically. Its [release policy](https://www.rabbitmq.com/release-information)
lists community support for 4.3 through 2026-11-30. Commercial support is not
assumed. [Windows installation guidance](https://www.rabbitmq.com/docs/install-windows)
requires x64 Erlang, careful service/cookie configuration and compatible paths.
An existing incompatible Erlang installation blocks unattended takeover.

[NSIS 3.13](https://nsis.sourceforge.io/Download), released 2026-09-27, is a build-time
candidate. Its x86 compiler installer and SHA-256 are recorded from an
[immutable Microsoft WinGet manifest](https://github.com/microsoft/winget-pkgs/blob/ca8886c3aef0edcbb4f29552f23a4a5b9599305f/manifests/n/NSIS/NSIS/3.13/NSIS.NSIS.installer.yaml).
This registry metadata is not a vendor signature or locally verified compiler.
No tool was installed, and the compiler installer is not a target-machine
prerequisite. Bootstrap architecture, WPF launch and unattended behavior still
need qualification. Review its [module-specific licenses](https://nsis.sourceforge.io/License)
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
as minimum requirements. The inventory records selected payload sizes;
FIN-280's qualified release metadata must include
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

## Release Qualification Handoff

Owner-approved phase split, 2026-10-09: FIN-272 delivers this versioned inventory,
the tested non-release validator/offline inspector, pinned evidence, ownership
and explicit unknowns/release blockers. Complete package qualification is a later
release gate, not a prerequisite for closing this inventory ticket. This replaces
earlier instructions requiring full qualification before FIN-272 completion.

| Required later evidence | Delivery owner |
| --- | --- |
| Safe package verification/enforcement, prerequisite consent, resource preflight, owned unattended install/reboot/resume | FIN-278 |
| Qualified compiler/bootstrap, final signatures/trust, native prerequisites, notices/EULAs/SBOM, redistribution and support metadata | FIN-280 |
| Separate actual Windows 11 / Server 2022 / Server 2025 lifecycle and negative-test matrix | FIN-281, with FIN-206 final acceptance |
| SCM hosting, safe wrappers/identities, gateway wiring and native adapters | FIN-273 / FIN-277 and their existing dependent gates |

Every unresolved item below is handed off to those owners. Inventory completion
never approves a payload, permits privileged installation, waives a release gate
or changes `releaseReady: false`. Additional-spend authorization remains zero.

Owner clarification, 2026-09-18: paid operations are excluded from POC development
and testing. Final installed-POC testing is owner-run after development. Continue
the manifest, implementation and free verification; prepare reproducible scripts
and explicit pending checks for handoff. Do not buy a signing certificate, activate
a paid provider or provision paid test hosts to unblock this inventory. Metadata,
integrity and security requirements still apply; untested is not approved.

1. Complete PostgreSQL 18.6-5 native dependency, notice and application-control
qualification; the exact archive digest has been verified locally.
2. Qualify the pinned search/observability candidates, service wrapper and support/license matrix.
3. Complete all-content trust, notices and redistribution review for the twelve
   digest-verified payloads; capture exact unattended commands, exit codes and
   resource requirements. NSIS compiler payload verification remains separate.
4. Resolve bootstrap/compiler qualification and the approved product-signing path.
5. Publish the separately qualified release matrix through CI/review in FIN-280,
   with FIN-278 enforcement and FIN-281/206 installed-target evidence. FIN-272's
   inventory merge/closure is not that release approval.

No additional spending, package/tool installation or machine changes are authorized
by this document. Current POC completion is 192/215 (89.3%) after FIN-285 closure,
+0.5 percentage points before rounding; FIN-270 is 10/19 children Done and
FIN-38 P9 is 16/39 (41.0%). Historical FIN-277 closure is191/21588.8%.
Historical FIN-284 closure is190/21588.4%.
Historical FIN-275 closure is189/21587.9%.
Historical ownership decomposition is188/21587.4%,
-1.2 pp for granularity only. Historical FIN-283 Profile/Category closure
is 188/212 (88.7%); historical FIN-282 Identity closure
is 187/212 (88.2%); historical FIN-274 foundation closure
is186/212 (87.7%); historical FIN-273 closure is185/207 (89.4%). The five-leaf
persistence decomposition preserved the original domain requirements.
No delivered work was lost. First-user testing
is Not Ready. See [the progress ledger](../../docs/agent/POC_PROGRESS.md).
