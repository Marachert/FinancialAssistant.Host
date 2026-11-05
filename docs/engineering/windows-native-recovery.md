# Native Portable Recovery

FIN-209 is In Progress. This increment provides authenticated portable artifact
encryption and separately isolated Income/Expense, Profile/Category, Identity, Intake and Audit restore
drills. It is not a
complete POC backup utility, installed-host qualification or permission to restore
an existing database. No completion credit or first-user readiness is claimed.

[PR303](https://github.com/Marachert/FinancialAssistant.Host/pull/303) is independently
verified merged=true/CLOSED: actual protected squash
`379e6fc630b62aa0f1f269188db89c3e45ac66bd` at `2026-11-03T08:21:44Z`, final head
`4515a8229b89a43a0e94866d2972369b84df8237`. All five exact-head checks passed
(Backend41600143931/Mobile41600143848/Admin41600143814); configured review completed
`2026-11-03T08:03:10.597513Z` with no findings. All final review channels and labels
were clear; nine pre-merge Confluence full-body readbacks were verified. This is
an implementation merge, not FIN-209 Done or complete backup qualification.

The package follow-on [PR306](https://github.com/Marachert/FinancialAssistant.Host/pull/306)
is independently verified merged=true/CLOSED: actual protected squash
`ce26f8344982b884e2f5ba0cb4c01fc1b6fd5ce6` at `2026-11-03T09:35:20Z`, final
`ab4297866f02158bd4ee8d1573e68bdab7e74723`. All five exact-head checks passed
(Backend41610686585/Mobile41610686653/Admin41610686645); configured review completed
`2026-11-03T09:25:17.152848Z` without findings. Final channels/labels were clear;
seven affected pre-merge Confluence bodies and central actual-merge bodies were
verified. Package IO is not FIN-209 closure or complete business recovery.

The manifest follow-on [PR305](https://github.com/Marachert/FinancialAssistant.Host/pull/305)
is also independently verified merged=true/CLOSED: actual protected squash
`edc4a76155d68ebed032bbca06845de7b810c000` at `2026-11-03T08:57:26Z`, final
`5dc07721b8dcef2bccd8a7228ed0810e414481d7`. All five exact-head checks passed
(Backend41606135388/Mobile41606135402/Admin41606135375); configured review completed
`2026-11-03T08:50:30.176799Z` without findings. Final channels/labels were clear;
seven affected pre-merge Confluence bodies and central actual-merge bodies were
verified. FIN-209 remains In Progress without completion credit.

Follow the [native deployment decision](../architecture/windows-native-poc.md),
[protected configuration contract](windows-native-protected-configuration.md),
[persistence qualification](native-persistence-qualification.md) and
[owner inventory](native-financial-persistence-scope.md).

The protected destination follow-on [PR307](https://github.com/Marachert/FinancialAssistant.Host/pull/307)
is independently verified merged=true/CLOSED: actual protected squash
`938ba7d3af08a015ab22178e497fe9da79cfec61` at `2026-11-04T10:10:10Z`, final
`02f0a83d0bfb63a76c0b2f55bb872dfe2a29b951`. All five exact-head checks passed
(Backend41810861267/Mobile41810861173/Admin41810861214); configured review completed
`2026-11-04T09:58:20.226277Z` without further findings. The valid abandoned-file
finding was fixed, regressed and answered with commit/test/CI evidence before its
thread was resolved after green. Final channels/labels were clear; five affected
Confluence bodies and two central actual-merge bodies were verified. The earlier
full local1557/1557 and final scoped Windows105/105 are separate results. No leaf
closure, completion credit or installed recovery acceptance is claimed.

## Portable Protection

The separately scoped Intake follow-on
[PR311](https://github.com/Marachert/FinancialAssistant.Host/pull/311) is independently
verified merged=true/CLOSED: actual protected squash
`b0045e0a24ddfdb7a23cdf382654ca102185c4c6` at `2026-11-04T23:33:56Z`, final
`177b3c6c45dd5e7a99d9d504fa6d5346b756ef26`. All five exact-head checks passed
(Backend41933146568/Mobile41933146592/Admin41933146549), decoded CI1619/1619
across26 projects, no failures/skips. Review completed
`2026-11-04T23:23:33.864792Z` without findings, final channels/labels clear and
post-merge Delivery194/Bootstrap582 fully read-back verified. FIN-209 stays open.

The separately scoped Identity follow-on
[PR310](https://github.com/Marachert/FinancialAssistant.Host/pull/310) is independently
verified merged=true/CLOSED: actual protected squash
`4f60aef945c09278fcdb86f63ce99583aea6687d` at `2026-11-04T22:50:26Z`, final
`2d01458b2243b958f44d4a86b40b3a1670296cb6`. All five exact-head checks passed
(Backend41922585385/Mobile41922585390/Admin41922585560), decoded CI1618/1618
across26 projects, no failures/skips. Review completed
`2026-11-04T22:09:51.055926Z` without findings, final channels clear and post-merge
Confluence Delivery191/Bootstrap579 fully read-back verified. FIN-209 stays open.

The separately scoped Profile/Category follow-on
[PR309](https://github.com/Marachert/FinancialAssistant.Host/pull/309) is independently
verified merged=true/CLOSED: actual protected squash
`53c0a3f41ce20c871a1a8231a596ac3c00eff9f4` at `2026-11-04T21:28:32Z`, final
`4f08be7332c4050c59e35f68d1440e19512e1540`. All five exact-head checks passed
(Backend41914728090/Mobile41914728014/Admin41914728194), decoded CI1617/1617
across26 projects, no failures/skips. Configured review completed
`2026-11-04T21:18:59.878229Z` without findings and final channels/labels were clear.
Post-merge Delivery188/Bootstrap576 were fully read-back verified. FIN-209 is
still open; this implementation merge adds no completion credit.

The coordinated capture follow-on [PR308](https://github.com/Marachert/FinancialAssistant.Host/pull/308)
is independently verified merged=true/CLOSED: actual protected squash
`e469e2b1bd7c27046078a4656af0802cfd850ead` at `2026-11-04T20:39:29Z`, final
`5b1ac33fa8189f7f5f05b6d2ae6dabac70ff01d9`. All five exact-head checks passed
(Backend41816672625/Mobile41816672779/Admin41816672565); decoded backend logs
verified 1,614/1,614 tests across26 projects without failures/skips. Configured
re-review completed `2026-11-04T10:51:01.849646Z` without findings; final reviews,
threads, comments and labels were clear. The initial TLS fixture failure remains
failed; the minimal test-only repair passed fresh fixing-head CI without changing
production limits. Central post-merge Confluence bodies were verified at
Delivery185/Bootstrap573. FIN-209 remains open without completion credit.

`FinancialAssistant.Shared.Recovery` uses .NET 8 cryptographic APIs, without a new
external cryptography package or paid secret service:

- A fresh random 256-bit recovery key encrypts each artifact with AES-256-GCM,
  an independently random 96-bit nonce and a 128-bit authentication tag.
- Versioned authenticated context binds source installation, backup, artifact,
  canonical resource owner and resource kind. Moving ciphertext between owners,
  purposes, installations or backup generations fails authentication.
- The recovery key is independently wrapped with AES-256-GCM using a key derived
  from operator unlock material by PBKDF2-HMAC-SHA256, 600,000 iterations and a
  fresh 256-bit salt. The version fixes the work factor; untrusted envelopes
  cannot downgrade it or select an arbitrary expensive value.
- Unlock input is bounded to 32-1,024 bytes. Length does not establish entropy:
  release provisioning must supply independently generated high-entropy material
  and keep it separate from the backup. No password or unlock value belongs in
  command arguments, a manifest, logs or the repository.
- One plaintext artifact is bounded to 64 MiB; the format is not a streaming or
  arbitrary-size archive. Larger input must be refused until a separately tested
  authenticated chunking design exists. It must never be silently truncated.
- Temporary mutable key/plaintext buffers are cleared. Exceptions omit sensitive
  details and inner provider errors. This is not a guarantee that all managed
  strings, OS caches or deleted file contents can be erased.

Machine DPAPI remains useful for the installed configuration, but copying a
DPAPI blob is not portable recovery. A future privileged capture path must read
authorized source secrets and independently protect their portable form; target
activation must re-protect them for its own installation/service/SID. Never copy
source machine ownership or bypass destination artifact validation.

## Authenticated Inventory

The follow-on manifest component is not wired to an installer capture engine.
`NativeRecoveryManifest` requires exactly 30 owned artifact descriptors:
13 databases, 16 configuration bundles (15 native hosts plus
the separate Summary module) and one encrypted receipt-object bundle. Empty
receipt collections still require an explicit bundle; omission is not evidence
that no receipts exist. Each artifact is bounded to 64 MiB of plaintext; a larger
database or receipt collection is unsupported until a tested chunking format exists.

The encrypted manifest binds its own source installation/backup/manifest IDs to
the authenticated outer context. It records an opaque consistency-boundary ID,
UTC capture timestamp, application version, installer-manifest digest, PostgreSQL
major version 18, drained-then-recreate broker policy and rebuild-from-owned-state
search policy. Each resource has a unique nonempty ID, canonical owner/kind,
version 1 format, exact ciphertext length and SHA256; database descriptors also
require the captured migration checksum. Unsupported schema/policy versions,
duplicate or case-ambiguous JSON keys, unknown properties, incomplete/extra
coverage and malformed metadata fail closed. The metadata document is bounded
to 64 KiB; returned descriptors and exported bytes cannot mutate the validated plan.

Consumers of untrusted input must use authenticated `Open`, not treat a locally
created document as independently verified evidence. `OpenArtifact` checks the
manifest-bound ID/length/digest and then authenticates ciphertext under its exact
owner/kind/context before returning plaintext. Hashes do not replace AEAD.

This validates authenticated inventory metadata and artifact identity, not the
truth of a producer's claims. A boundary GUID does not stop writers or drain a
broker; a descriptor/checksum does not inspect SQL migration content, retained
keys or receipt metadata. The trusted capture/restore engine must verify those
facts and exact bundle coverage against actual owned state. The existing scoped
two/four/six-artifact drills are deliberately rejected as a complete inventory.
Unit-test synthetic 30-artifact roundtrips are not whole-application restore proof.

Follow-on local verification: all **1,528/1,528** tests across26 projects passed,
zero failures/skips, from `2026-11-03T08:42:09.2828873Z` to
`2026-11-03T08:45:55.7765747Z`; recovery75/75 includes44 manifest regressions.
Full restore/Release build passed with zero errors and five existing warnings.
These results do not assert a new physical recovery drill or exact-head CI.

## Portable Package Streams

`NativeRecoveryPackage` constructs and independently reads a portable encrypted
package using the full inventory contract. The caller supplies all 30 plaintext
captures with migration metadata and a metadata prototype with no prebuilt
artifact entries. The factory validates complete coverage before encryption,
generates independent artifact IDs and a random recovery key, encrypts each
artifact, authenticates the complete manifest, wraps the recovery key separately
and clears its local plaintext key. Inputs remain caller-owned and must be cleared
by that caller; capture strings and mutable buffers must never be logged.

The binary version1 format contains an opaque identity/key-wrap/length header,
the encrypted bounded manifest, ciphertext artifacts in canonical manifest order
and a final completion marker. Import requires the exact declared total length,
valid IDs/versions/unlock material, authenticated manifest and every authenticated
artifact before returning any package. Truncation, trailing bytes, tampering,
wrong unlock material and cancellation cannot produce a usable partial result.
The completion marker alone is not proof of integrity or business recovery.

One package is bounded to 128 MiB, with the existing 64 MiB per-artifact limit.
Creation conservatively reserves the maximum manifest size before allocating
ciphertext. Oversize input is refused; no truncation or streaming/chunking claim
is made. Stored package objects retain only ciphertext and immutable metadata,
not the recovery key. Each explicit artifact-open unwraps a temporary key and
clears it in `finally`; the caller owns and must clear returned plaintext.

`WriteTo` accepts only an empty seekable destination at position0; it never
truncates, resumes into or overwrites existing bytes. It writes the completion
marker last, flushes the stream and uses `FileStream.Flush(true)` for actual files.
`ReadFrom` requires a bounded seekable source at position0. Caller streams remain
caller-owned. These APIs do not choose paths or create/repair ACLs. The installer
must open a new exclusively owned local temporary file through validated handles,
validate destination/ancestor ownership and capacity, and publish it atomically
only after a successful durable write and independent verification. On failure,
preserve/clean only journal-proven owned temporary output; never overwrite an
existing backup, report a partial file as success or delete the last verified
recovery point. Secure destination provisioning/publication remains FIN-278/280.

Free tests use one new owned GUID directory and `FileMode.CreateNew`/exclusive
file handles with synthetic payloads. They verify actual encrypted-file roundtrip,
no plaintext payload/unlock in file bytes, wrong-key/tamper/truncation/trailing
data denial, existing-destination preservation, mid-write failure, cancellation
and disposal. They do not claim actual all-owner SQL/object/configuration capture,
receipt/migration/key-content reconciliation or complete business restore.

Local package verification passed **1,551/1,551** tests across26 projects, zero
failures/skips, from `2026-11-03T09:15:29.9330208Z` to
`2026-11-03T09:19:42.0296714Z`; recovery98/98 includes23 package/file/fault tests.
Full Release build passed with zero errors and five existing warnings. These
are component tests with synthetic captures, not complete POC capture/restore,
installed-host acceptance or a claim of exact-head CI.

## Protected Windows Destination

`WindowsNativeRecoveryStore` reuses the existing Windows protected-directory
policy: local fixed NTFS, pinned non-reparse ancestors, same-handle file type/link
and ACL checks, matching current operator SID, protected private root DACL and an
exclusive writer lease. The root must already exist with approved ownership;
the store never provisions, broadens or repairs permissions. It is a library,
not another Windows service. The installer must validate that the dedicated
destination is separate from application/source data and is operator-approved.

Save independently authenticates the complete package before writing, derives an
opaque filename from source-installation/backup IDs, uses the existing flushed
temporary-file/atomic-no-overwrite write, and verifies persisted length/digest
against the independently authenticated bytes before reporting storage success.
Identical retries are accepted without rewriting. Conflicting or corrupt existing
bytes are preserved and rejected, never silently replaced. Open requires the
expected source/backup identities and authenticates the whole stored package.
Wrong unlock material, unexpected identities, corruption and unsafe directory
policies cannot return a usable package.

The helper's original three-argument constructor and 12 MiB default file limit
remain unchanged. Only an explicit bounded four-argument overload permits up to
128 MiB for this backup consumer; receipt-object limits and ACL/identity checks
are not weakened. Operations are serialized inside the existing cross-process
lease. Await active work before synchronous disposal, or use `DisposeAsync`;
busy synchronous disposal refuses to block a UI thread. Mutable serialized
ciphertext/readback buffers are cleared after use.

After acquiring the exclusive lease, startup removes only validated abandoned
opaque `.tmp` files through the existing no-follow temporary-file API. It never
removes or replaces published `.object` archives. A restart regression verifies
temporary-file recovery while preserving the stored archive digest and its
authenticated reopen. The review-fix recovery suite passed105/105 on Windows;
that scoped result is separate from the earlier full1,557-test baseline below.

No archive deletion, automatic retention, scheduler, root provisioning or semantic
capture/restore actuator is added. Destination capacity exhaustion fails closed;
the last verified recovery point is never deleted to make room. Full capture,
migration/key/object reconciliation, installer lifecycle and approved-host business
acceptance remain mandatory. Windows tests use only new private GUID fixture
folders, including a real package above the legacy 12 MiB limit, duplicate/conflict,
corruption, wrong unlock, broad ACL/second-writer denial, cancellation and cleanup.
Non-Windows tests assert platform refusal rather than claiming those positives.

Local verification passed **1,557/1,557** tests across26 projects, zero failures/
skips, from `2026-11-03T09:53:06.7183991Z` to `2026-11-03T09:57:01.8115216Z`;
recovery104/104 includes six actual Windows protected-store scenarios. Full Release
build passed with zero errors and five existing warnings. The existing native
adapter owned-process crash/restart, exact ciphertext/10 MiB receipt, portable
restore/deletion, diagnostic recovery and junction rejection also passed against
the rebuilt helper. This is local synthetic component evidence, not installed
SCM, power-loss durability, complete business backup or exact-head CI.

## Coordinated Capture Boundary

`NativeRecoveryCaptureCoordinator` orders the trusted privileged source adapter:

1. Validate/snapshot complete deployment metadata and unlock bounds before mutation;
   acquire the source-installation/boundary-bound exclusive installation lease.
2. Freeze new ingress and scheduled/external work, drain the broker, then stop
   all writers. Draining precedes stopping the consumers needed to finish delivery.
3. Inspect source ownership, exact stopped-writer coverage for all15 hosts plus
   Summary, verified owned broker topology, zero ready/unacknowledged deliveries,
   zero database clients and zero receipt writers.
4. Capture each of the30 required resources in canonical order, checking identity,
   schema/migration metadata and individual/aggregate bounds. Re-inspect quiescence
   after every resource and after package construction. Foreign/missing/duplicate
   writers, foreign boundaries, stale/future/non-UTC observations, partial captures,
   resumed writers or pending broker work reject the entire result.
5. Return the complete encrypted package only after successful lease disposal.
   Clear every transferred plaintext buffer on success, failure or cancellation.

Inspection observations must be taken after both the stopped-writer boundary and
the current inspection request, not more than five seconds before validation,
and bound to the exact source/boundary IDs. A recent cached observation is refused.
Capture time comes from the coordinator clock, not a caller-provided timestamp.
The existing package limit reserves its maximum manifest and framing overhead
before retaining additional capture bytes. Adapter diagnostics are sanitized;
caller cancellation does not expose their messages or inner exceptions.

The adapter must acquire and journal ownership before mutation; inspect actual
SCM/process ownership, database sessions, object writers and the complete private
broker topology rather than merely assert booleans. It owns its intermediate
buffers until it transfers them through `NativeRecoveryCapturedResource`; after
transfer it must not mutate/reuse them. Failed calls clean their own untransferred
buffers. Every adapter operation needs a bounded timeout and must honor caller
cancellation; failed acquisition must clean its own untransferred lease. Lease
disposal must release only the owned lock, never restart services
or reopen ingress. A durable installer journal must retain the paused state and
perform separately verified controlled resume after success/failure. Process death
requires journal recovery, not this in-memory coordinator alone.

This library does not implement those Windows/broker/database adapters, scheduling,
destination provisioning, retention, restore activation or journal recovery. It
does not publish a backup or certify actual all-owner capture. Tests use synthetic
adapters to verify sequencing, drift/fault/cancellation refusal, disposal and buffer
clearing; they are not installed-service or full-business recovery evidence.

Local verification: full baseline **1,613/1,613** across26 projects, zero failures/
skips, from `2026-11-04T10:26:36.7773083Z` to `2026-11-04T10:31:22.9198064Z`.
The final cached-observation guard and its regression then passed the complete
scoped recovery suite **161/161**, zero failures/skips. These are separate runs,
not a newly claimed full1,614-test result. The baseline Release build passed with
zero errors/five existing warnings; final scoped build passed zero warnings/errors.
The56 new orchestration cases cover sequencing, complete encrypted roundtrip,
faults, cancellation, stale/foreign/drifting evidence, malformed resources,
individual/aggregate buffer limits, metadata snapshots and plaintext cleanup.

Initial PR308 CI correctly blocked merge because an existing HTTPS test raced the
server's early413/connection-close response with a known-length body upload on
Linux. Its test-only companion repair uses `Expect: 100-continue`, asserts that
the oversized body is never serialized, and retains strict413/connection-close,
real boundary-sized upload and real chunked/proxied overflow checks. The fixture's
continue wait exceeds its bounded request timeout, preventing fallback body writes.
This follows the [.NET8 handler's final-error behavior](https://github.com/dotnet/runtime/blob/v8.0.0/src/libraries/System.Net.Http/src/System/Net/Http/SocketsHttpHandler/HttpConnection.cs#L619-L660).
No production TLS behavior, body limit, security assertion or test is removed.
The original failed CI result remains failed; only a fresh fixing-head run can
satisfy the merge gate.
The companion repair passed the actual Windows loopback gateway suite80/80,
scoped build with zero warnings/errors, changed-source format, privacy1337,
Markdown160/630 and diff checks. Recovery161/161 is unchanged; no new full local
suite or Linux success is inferred from these scoped Windows results.

## Repeatable Owned Drill

On Windows, using PowerShell 7, .NET 8 and the locally available PostgreSQL archive
pinned by [the component manifest](../../infra/windows-native/component-manifest.json):

```powershell
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
pwsh -NoProfile -NonInteractive -File tools/scripts/test-owned-postgres.ps1 -Scenario Recovery -ArchivePath <local-pinned-postgresql-archive.zip>
pwsh -NoProfile -NonInteractive -File tools/scripts/test-owned-postgres.ps1 -Scenario Recovery -RecoveryScope ProfileCategory -ArchivePath <local-pinned-postgresql-archive.zip>
pwsh -NoProfile -NonInteractive -File tools/scripts/test-owned-postgres.ps1 -Scenario Recovery -RecoveryScope Identity -ArchivePath <local-pinned-postgresql-archive.zip>
pwsh -NoProfile -NonInteractive -File tools/scripts/test-owned-postgres.ps1 -Scenario Recovery -RecoveryScope Intake -ArchivePath <local-pinned-postgresql-archive.zip>
pwsh -NoProfile -NonInteractive -File tools/scripts/test-owned-postgres.ps1 -Scenario Recovery -RecoveryScope Audit -ArchivePath <local-pinned-postgresql-archive.zip>
```

The runner verifies the archive digest before use. It creates a user-only owned
GUID directory, starts only a temporary loopback PostgreSQL process on a dynamic
port, verifies `SHOW data_directory`, and uses synthetic credentials and data.
It installs no Windows service, vendor installer, certificate or firewall rule.
Do not run the integration executable directly against a shared cluster.

`RecoveryScope` is explicitly allowlisted: `IncomeExpense` is the default;
`ProfileCategory` selects a separate two-owner fixture; `Identity` selects one
separate owned database and its bounded fixture configuration.
`Intake` selects the fixture's Transaction Intake, Income and Expense schemas and
three separate64-byte data-key artifacts, six artifacts in total.
`Audit` selects the Audit fixture and its empty Score isolation counterpart,
four schema/key artifacts; this does not prove Score business recovery. Supplying this option to
any other scenario is rejected before fixture creation or archive use. The runner
canonicalizes/saves/restores its process environment and checks both phase proofs
against the selected scope before setting `businessRecoveryVerified=true`.
The parent result records that scope. A scope tag is not complete-POC acceptance.

The explicit Recovery scenario:

1. Initializes the selected Identity, Income/Expense, Profile/Category, Intake or Audit schemas, migration/runtime
   roles and keys, using that scope's existing actual persistence assertions.
2. Waits for the source business process to exit and rejects active source client
   sessions. It dumps each owned schema separately from this trusted synthetic
   source, preserving its migration/version metadata and ownership grants.
3. Encrypts each owned dump and its configuration/key bundle, wraps the
   random recovery key with independently generated operator material, then
   clears the original key and deletes plaintext dumps.
4. Independently reads and verifies the exact scope-bound catalog (two artifacts
   for Identity, four for Income/Expense, Profile/Category or Audit, six for Intake), unwraps
   the key, authenticates each artifact and creates a new empty restore database.
   Existing targets are rejected. The fresh target revokes default PUBLIC schema
   access; native ownership checks are not weakened to accept a restore.
5. Runs trusted `pg_restore` with `--exit-on-error --single-transaction`, retaining
   schema ownership and grants. It does not clean, drop or overwrite a store.
6. Proves incorrect recovered keys fail at owned reads, then reinstates the actual
   recovered keys. Income/Expense verifies exact snapshot hashes, EUR/USD totals,
   owner/role isolation and immutable confirmation replay without extra outbox
   records. Profile/Category verifies exact decimal preferences, financial-settings
   revision, taxonomy/aliases/version/time, deleted-owner markers, snapshot hashes,
   pending event count and independent SQL role denial without changing those values.
   Profile/Category also republishes the identical recovered event, rejects a
   conflicting replay and proves the full pending-event and business snapshot
   hashes remain unchanged. Identity rejects both duplicate and conflicting
   re-enqueue attempts, preserving the original outbox row revision/payload.
7. Performs an actual immediate PostgreSQL crash and restart, then repeats source
   and restored-business checks in new processes, including wrong-key denial.

Both phase proofs must explicitly pass before `businessRecoveryVerified=true`.
The new database is in the same owned temporary cluster and reuses its synthetic
roles. This does not qualify replacement-host role provisioning, real DPAPI secret
export or installer activation. The source bundles contain only synthetic fixture
configuration and key material.
The parent stops only its own PID/image/start-time-matched process, removes its
temporary credential file and verifies cleanup. Decrypted key/dump files are
removed in `finally`; retained private fixture evidence must never be committed.
An early failure is not a passed restore. A timeout or unrelated child failure
cannot satisfy the expected wrong-key rejection.

Evidence is under ignored `.codex-runtime/persistence-test/<guid>/`: parent
`result.json`, `recovery-initial.json`, `recovery-restart.json` and private logs.
The catalog contains synthetic owner/ID/digest metadata and a wrapped key, not an
installer-ready authenticated manifest. `installed=false` and
`releaseReady=false` remain mandatory. This drill is separate from the nine
scenarios in the full persistence matrix; these recovery scopes do not inflate it.

### Identity Configuration And Authentication

The Identity source exports the actual tested compatible fixture options at the end
of initialization into a new private temporary file, bounded to64KiB. This includes
database index/encryption keys and the five stable lookup, refresh-hash, access-signing,
provider-identifier and event-subject keys. The captured configuration is the tested
90-day refresh/120-day hard-ceiling variant, not the intentionally incompatible
cleanup probe. The parent encrypts this source file as the Identity configuration
artifact and removes plaintext; source/target buffers are cleared where mutable.
Managed strings and filesystem caches cannot be guaranteed erased.

The restore fixture strictly deserializes the bounded configuration, validates the
owned source/port/credentials, native storage mode and retained ceiling, then rebinds
only the known empty target database/credentials. Google/Apple/phone stay disabled
and events remain durable pending transport; no real provider call occurs. Negative
key testing changes only data keys in otherwise valid JSON so a parse failure cannot
stand in for data-key rejection. This is synthetic fixture configuration, not real
DPAPI export, production secrets or replacement-host account provisioning.

Restored known accounts/roles/credentials, revoked session family, locked challenge,
foreign-owner denial and pending outbox state are checked before/after actual crash.
Duplicate and conflicting outbox inserts must fail without changing the original
row revision/payload. The runtime role cannot read PostgreSQL credential metadata.
A real native email sign-in uses the recovered configuration; the existing JWT
library verifies issuer, audience, lifetime, HS256 signature and subject/session
claims. This check runs inside an outer transaction deliberately rolled back after
validation; the newly issued session must be absent afterward. No token is printed
or retained in evidence, and authentication must not silently mutate the recovery
fixture to make later replay checks pass.

Local Identity-increment full verification passed **1,618/1,618** tests across26
projects, zero failures/skips, from `2026-11-04T21:59:15.9709683Z` to
`2026-11-04T22:04:26.4849056Z`. Repository342/342 includes the additional Identity
wrong-scenario guard. Full restore and Release build passed with zero errors and
five existing warnings; the integration-only build passed with zero warnings/errors.
This is local verification, not exact-head CI or installed-host acceptance.

The actual Identity restore and immediate-crash/restart phases passed with cleanup
at `2026-11-04T21:42:54.5522512Z`. This remains a separate one-owner cluster, not
one atomic five-owner backup with the earlier scopes, complete13-owner capture,
complete30-resource packaging or installed acceptance.

Current Identity-increment regressions also passed: normal Identity persistence
with crash/restart and cleanup at `2026-11-04T21:44:49.4349655Z`, Profile/Category
recovery with both phase proofs and cleanup at `2026-11-04T21:47:26.3472511Z`,
and default Income/Expense recovery with both phase proofs and cleanup at
`2026-11-04T21:51:48.6652308Z`. Normal Identity correctly records
`businessRecoveryVerified=false`; only the explicit recovery scenarios claim
business restore verification. No plaintext captured/recovered key or dump file
remained in the Identity recovery root after cleanup.

The Profile/Category scope passed both actual restore and immediate-crash/restart
phases on `2026-11-04T21:02:14.3977960Z`; parent cleanup was verified. Its evidence
explicitly records `scope=profile-category`, portable keys, wrong-key rejection,
business reconciliation and immutable replay, with `installed=false` and
`releaseReady=false`. This is a separate owned cluster from Income/Expense, not
one atomic four-owner backup, complete13-owner capture or installed acceptance.
The existing Income/Expense recovery scope was rerun successfully at
`2026-11-04T20:52:47.3928887Z`, including both wrong-key/business/restart phase
proofs and cleanup. The original non-recovery Profile/Category persistence scenario
also passed with actual crash/restart, event replay/conflict assertions and cleanup
at `2026-11-04T21:03:49.6809678Z`;
its result correctly keeps `businessRecoveryVerified=false`, rather than claiming
that ordinary persistence verification performed a restore. These are local
synthetic fixture results, not exact-head CI or installed lifecycle qualification.

Historical Profile/Category-increment full verification passed **1,617/1,617** tests across26
projects, with zero failures/skips, from `2026-11-04T21:07:47.6821233Z` to
`2026-11-04T21:12:33.8319768Z`. Repository341/341 includes three new negative
tests proving `RecoveryScope` cannot be silently ignored by other scenarios.
Full restore and Release build passed with zero errors/five existing warnings;
the final scoped integration build passed with zero warnings/errors. These are
local results, not a claim that CI or installed acceptance has already passed.

### Intake And Financial Owners

The explicit Intake scope captures its fixture's Transaction Intake, Income and
Expense schemas only after the source process exits and no source database client
remains. Existing `StorageIdentity` maps service IDs to validated schema/runtime/
migration identifiers, including `transaction-intake` to `fa_transaction_intake`;
do not build SQL identifiers from untrusted archive owner strings.

The target requires all three recovered key sets, restores a new empty database
without migrations/overwrite, and checks exact draft/confirmation, USD2500 income,
EUR25 expense and outbox hashes. The bounded raw outbox page must contain fewer
than100 rows or fail rather than silently truncate the proof; cursor-maintenance
metadata is not confused with event payload/revision immutability. Repeated intake
and confirmation requests, both financial consumers and identical outbox publish
cannot change these hashes; conflicting outbox publish must fail. Foreign-owner
lookups and all three cross-role SQL reads are denied. Deleted source payloads
must remain physically absent rather than be resurrected by replay. These checks
also run in the normal non-recovery Intake restart scenario.

This trusted synthetic three-owner boundary is separate from the earlier clusters,
not a common six-owner or complete thirteen-owner snapshot. It captures fixture
data-key material, not all real protected configuration, receipts, external work or
installed replacement-host provisioning. The actual Intake restore and immediate-
crash/restart phases passed with cleanup at `2026-11-04T22:58:28.8772565Z`;
both structured phase proofs explicitly record scope, portable keys, wrong-key
denial, business reconciliation and immutable replay. `installed=false` and
`releaseReady=false` remain mandatory.

Normal Intake passed with cleanup at `2026-11-04T23:01:18.1478350Z` and correctly
keeps `businessRecoveryVerified=false`. Fresh Identity, Profile/Category and default
Income/Expense recovery regressions passed at `2026-11-04T23:02:39.4502908Z`,
`2026-11-04T23:03:56.0863271Z` and `2026-11-04T23:05:13.8261510Z` respectively.
An earlier private wrapper reported failure after its fixture passed because it
checked an unset native exit code; that wrapper result remains failed. The wrapper
was corrected to invoke a separate PowerShell process and the normal run above
passed with actual exit0, without changing or weakening tracked tests.

Local full verification passed **1,619/1,619** across26 projects, zero failures/
skips, from `2026-11-04T23:12:43.5305679Z` to `2026-11-04T23:17:39.3461300Z`.
Full restore/Release build passed with zero errors and five existing warnings;
integration-only build had zero warnings/errors. Full formatting verified0/1299
files, and final documentation tests passed21/21. Exact-head CI and review remain
required before merge. No installed or complete POC recovery is inferred.

### Audit History And Replay Receipts

The explicit Audit scope preserves Audit metadata plus its fixture's empty Score
schema for independent role isolation. Each uses its actual distinct synthetic
index/encryption keys; non-adjacent key bytes are represented explicitly in the
allowlisted scope table rather than inferred. The empty counterpart must pass
native readiness and contain zero rows. This is not Score business recovery.

The source writes an internal encrypted snapshot proof after its actual Audit
assertions. This covers every `audit_entries` and `audit_receipts` keyset cursor,
revision and payload digest in deterministic order. Each bucket has at most100
pages and the combined snapshot at most10000 items; incomplete scans fail closed.
No raw metadata or digest inventory is printed or committed. The target must match
the source digest before and after actual replay/retention checks, then repeat
after PostgreSQL crash/restart. Original timestamps/expiry, minimum-retention
policy, revoked-admission historical reads,1000-entry correlation capacity,
physical expired-payload absence and conflict denial remain required.

This verifies Audit history and retained replay receipts, not all configuration,
objects, projections, broker state or a cross-resource backup. Actual new-scope
restore and immediate-crash/restart phases passed with cleanup at
`2026-11-04T23:44:31.0064620Z`. Both phase proofs explicitly record Audit scope,
portable keys, wrong-key denial, business reconciliation and immutable replay.
`installed=false` and `releaseReady=false` remain mandatory. Compilation alone
is not recovery or installed-host acceptance.

Normal Audit passed with actual crash/restart and cleanup at
`2026-11-04T23:45:54.8470256Z`, correctly keeping `businessRecoveryVerified=false`.
The four earlier recovery scopes passed again after the scope-table refactor:
Identity `2026-11-04T23:49:14.2915644Z`, Profile/Category
`2026-11-04T23:50:27.7120937Z`, default Income/Expense
`2026-11-04T23:51:53.6264120Z`, and Intake
`2026-11-04T23:53:21.4748390Z`; each parent records successful cleanup and recovery.
These are distinct owned clusters, not one cross-owner recovery point.

Local full verification passed **1,620/1,620** across26 projects, zero failures/
skips, from `2026-11-04T23:59:40.4935673Z` to `2026-11-05T00:05:17.2758050Z`.
Repository344/344 includes the new Audit wrong-scenario guard. Full restore and
Release build passed with zero errors/five existing warnings; the final scoped
integration build had zero warnings/errors. Full formatting verified0/1299 files
and final documentation checks passed21/21. Exact-head CI/review and actual
protected merge remain required; local results are not installed acceptance.

## Complete POC Recovery Gate

PR303 component verification on 2026-11-03: full Release build passed with zero errors and
five existing warnings; all **1,484/1,484** tests across26 projects passed, with
zero failures/skips. The actual Recovery drill passed at
`2026-11-03T07:45:57.9320335Z`, including both wrong-key checks and verified cleanup.
These are dirty-checkout component results, not exact-head CI, merge evidence or
approved-host acceptance. A preceding restore attempt failed because the fresh
target retained PUBLIC schema access; that failure was retained, the target was
corrected without weakening ownership checks, and a fresh isolated drill passed.

The following remain required before FIN-209 can close or FIN-278/280 can claim
integrated backup/repair support:

| Resource or boundary | Required integrated behavior |
| --- | --- |
| Consistency | Freeze ingress, stop publishers, drain known broker queues including unacknowledged deliveries, then stop consumers and other writers before cross-owner capture. A per-database dump is not a cross-resource snapshot. |
| All owned databases | Capture all thirteen business owners, including the Summary module, their schema/migration versions, encrypted state, durable outboxes/inboxes, deletion markers and retained decryption/index keys. Separate scoped drills cover Identity, Income/Expense, Profile/Category, Intake and Audit, not a common all-owner boundary. Audit's empty Score counterpart is not Score business recovery. |
| Receipt objects | Capture encrypted object bytes and matching receipt metadata under the same quiescence boundary; preserve retention/deletion semantics and reject missing or foreign objects. |
| Protected configuration | Capture required runtime and Summary module secrets, stable key sets and nonsecret deployment identity through authorized privileged reads. Authenticate a complete bounded manifest and exact artifact coverage. Never put unlock material beside the archive. |
| Broker and external work | A fresh broker is safe only after an actual empty/drained-queue proof and preserved durable pending work. Published-but-unconsumed messages must not be discarded. Preserve Identity pending transport, receipt jobs and uncertain provider state; do not resend paid work. |
| Search | Record an explicit versioned snapshot or rebuild policy. Search is derived, not financial truth; restore owner state and watermarks before enabling reads or replay. |
| Target activation | Refuse existing/shared targets by default, validate trusted artifact origins and capacity, provision least-privilege roles, re-protect target secrets and verify every business owner before reopening ingress. SQL archives are executable input, not safe foreign uploads. |
| Repair/update | Preserve secrets, object identity and owned state. Reject binary rollback across irreversible migrations; keep the pre-update recovery point until successful post-update business verification. |
| Retention/scheduling | Add bounded owner-authorized destination/retention and restart-safe native scheduling. No plaintext unattended unlock file, paid vault or silent loss of the last verified recovery point. |
| Acceptance | FIN-278/280 integration and approved-host restore/update/repair tests remain pending owner-run acceptance, separate from component tests and CI. |

Reference rationale: PostgreSQL documents
[single-database dump behavior](https://www.postgresql.org/docs/18/app-pgdump.html)
and [backup methods](https://www.postgresql.org/docs/18/backup.html); OWASP covers
[authenticated encryption and key management](https://cheatsheetseries.owasp.org/cheatsheets/Cryptographic_Storage_Cheat_Sheet.html)
and [PBKDF2 work factors](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).
These sources inform this bounded contract; they do not certify this project or
replace actual whole-application recovery evidence.
