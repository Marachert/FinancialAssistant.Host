# Native Persistence Qualification

FIN-286 is locally Verified Merged through PR297, actual protected merge
`d1704ab108892d8ee7007633066d5d9a21d751d5` at `2026-11-02T04:57:18Z`.
Jira reconciliation is pending. This is the no-cost database qualification
crosswalk, not installed Windows acceptance or a first-user readiness claim.
Follow the [storage policy](../architecture/storage-policy.md),
[ownership inventory](native-financial-persistence-scope.md) and
[live transport qualification](native-lifecycle-delivery.md).

## Repeatable Run

On Windows with PowerShell 7, .NET 8 and restored solution dependencies:

```powershell
dotnet restore FinancialAssistant.Backend.sln
pwsh -NoProfile -NonInteractive -File tools/scripts/test-native-persistence.ps1 -ArchivePath <local-pinned-postgresql-archive.zip>
pwsh -NoProfile -NonInteractive -File tools/scripts/test-native-adapters.ps1
```

Use the PostgreSQL archive pinned in
[`component-manifest.json`](../../infra/windows-native/component-manifest.json).
The runner verifies its SHA256 before creating a fixture, builds the current
checkout in Release with a twenty-minute cold-build deadline, then runs all nine scenarios
sequentially. Each scenario uses a fresh loopback cluster, synthetic database and
owner roles, SCRAM authentication and a user-only fixture directory. Each child
process has a two-minute deadline. No download, installer, machine service,
Docker/WSL, external provider or additional spending is involved.

Private evidence is retained under ignored
`.codex-runtime/persistence-qualification/<guid>/summary.json`, with an atomic
per-scenario JSON result and private build logs. Detailed fixture logs/data remain
under `.codex-runtime/persistence-test/<guid>`. Never commit or share those raw
directories: even synthetic fixture credentials and database contents are private.
The harness deletes its temporary password file and stops only a process whose
image, PID and start time match its own run. It does not recursively delete data.

A failed scenario stops the matrix. Every remaining scenario is `not-run`, never
skipped-as-passed. Missing, stale, wrong-scenario, wrong-digest, incomplete or
unclean evidence is rejected even when a console message says success. `passed`
requires all nine structured results and checked cleanup; `installed` and
`releaseReady` remain false. Build/restore failure is not a database pass.
The summary records the base Git head and whether the checkout has edits; a dirty
run must not be represented as verification of that unmodified commit.

The separate native-adapter command complements the PostgreSQL matrix with actual
owned file-process crash/restart, encrypted portable restore/deletion, abandoned
temporary-file cleanup and junction rejection. Its explicit exit status is
separate evidence, not a tenth database scenario or cross-resource backup proof.
See the [adapter guide](windows-native-adapters.md) for the protected file, expiry,
bounded cleanup and anti-resurrection contracts and Windows-only test boundaries.

FIN-209's separate [portable recovery drills](windows-native-recovery.md) capture
and restore encrypted schema/configuration artifacts into fresh targets for
separately selected Income/Expense, Profile/Category and Identity scopes, including
actual crash/restart reconciliation and wrong-key denial. Identity also verifies
native sign-in/JWT validation inside an explicit rollback. These do not replace
this nine-scenario matrix, combine into one five-owner boundary or establish
complete cross-resource backup or replacement-host provisioning.

## Owner Crosswalk

Each project below is under `backend/`; all initial scenarios rerun their owned
migration, then the harness performs an immediate PostgreSQL crash and starts a
new .NET process against the same cluster for exact persisted-state assertions.
See the linked implementation guides for detailed assertions and retained limits.

| Scenario / owners | Integration project | Representative enforced checks |
| --- | --- | --- |
| Foundation / shared technical storage | `shared/persistence/FinancialAssistant.Shared.Persistence.Integration` | Concurrent/idempotent migration, complete ACL/constraints, wrong schema/version/checksum/key denial, encrypted records, revision races, cancellation, lock timeout, deletion and live interrupted domain/outbox transaction recovery. |
| Identity | `services/identity/FinancialAssistant.Identity.Persistence.Integration` | Account/credential/session/outbox atomicity, independent-scope uniqueness, rotation/reuse, challenge replay, owner-bound deletion/keyset pages and bounded retention. [Guide](identity-native-persistence.md). |
| Profile, Category | `services/profile/FinancialAssistant.ProfileCategory.Persistence.Integration` | Exact settings/decimals/taxonomy/aliases, concurrent bootstrap/update, atomic state/outbox rollback, SQL role denial, erasure replay markers and pending-safe bounded retention. [Guide](profile-category-native-persistence.md). |
| Income, Expense | `services/income/FinancialAssistant.FinancialRecords.Persistence.Integration` | Independent runtime/migration roles and keys, exact records/currency, immutable confirmation replay, CAS/concurrency, atomic record/outbox rollback and pending-safe retention. [Guide](financial-records-native-persistence.md). |
| Intake | `services/transaction-intake/FinancialAssistant.Intake.Persistence.Integration` | Draft/review/confirmation races, atomic indexes/source/outbox, no financial write side effect, parser outside SQL, source expiry without resurrection and bounded pending-safe retention. [Guide](intake-native-persistence.md). |
| Receipt, AI orchestration | `services/receipt-processing/FinancialAssistant.ReceiptAi.Persistence.Integration` | Encrypted object identity, atomic candidate/intents, exact decimals, separate scopes/roles, durable quota/request replay and conservative uncertain-provider state. No actual provider call. [Guide](receipt-ai-native-persistence.md). |
| Summary, Analytics | `services/financial-summary/FinancialAssistant.Projections.Persistence.Integration` | Independent role denial, exact owner/currency totals, atomic source/projection/outbox, interrupted scoped rebuild, live watermark, bounded 1000-record generation and separate refresh intent. [Guide](native-projection-persistence.md). |
| Score, Recommendations/Notifications | `services/financial-score/FinancialAssistant.Insights.Persistence.Integration` | Owned facts/history/settings/preferences/publication, causal source order, stale/conflicting trigger denial, bounded 31-owner discovery, cursor restart, atomic replay and separate roles. Notification preparation is not provider delivery. [Guide](native-insights-persistence.md). |
| Audit | `services/audit/FinancialAssistant.Audit.Persistence.Integration` | Minimized append-only metadata, immutable replay/conflicts, historical reads after producer revocation, bounded physical expiry deletion and independent SQL role. [Guide](native-audit-persistence.md). |

These are thirteen business owners, not nine interchangeable business databases.
The shared schema/version/ACL/cancellation matrix qualifies the common technical
store; it does not assert that every negative test is duplicated for every owner.
Older isolated fixtures may reuse one synthetic password across test roles;
production provisioning still requires the independently protected credentials
defined by the native configuration policy.

## Migration And Recovery Boundaries

The physical storage schema is version 1. Compatibility qualification means fresh
creation, idempotent and concurrent rerun, strict existing manifest validation,
and explicit encryption-key addition while retaining old readable ciphertext.
Unknown version 2, wrong checksum/shape/grants and incompatible keys fail closed.
There is no implemented arbitrary historical-version upgrade or production key
removal migration; neither can be inferred from this matrix.

The Foundation interruption mode inserts both domain and outbox records inside a
live uncommitted owned transaction, creates a private readiness signal, and keeps
querying that transaction. Only then does the harness crash its PostgreSQL
process. It requires the probe to observe database interruption and the restarted
process to find both uncommitted records absent while the earlier committed
encrypted state remains. This differs from throwing an application exception
before a normal rollback or using a mock process restart.

The other scenarios check their actual committed/replayed/rebuild state after
database and application-process restart. This does not qualify power-loss
hardware behavior, SCM recovery, Windows account grants, installed backup/restore
or the Windows 11/Server 2022/2025 matrix. Those remain FIN-209/278/280/281/206.
Owner first-user bootstrap and Profile-only refresh remain explicit launch gaps.
Paid OCR/LLM/SMS/push execution is excluded, not marked successful.

## Current Evidence

The full 2026-11-02 matrix `d20f9ae6a3614934920b3e3a9bd83743` passed all nine
required scenarios with actual initial/crash/restart and checked cleanup,
verified `2026-11-02T04:19:49.4337636Z`. Foundation explicitly verified live
interrupted domain/outbox recovery. The current checkout built with zero errors;
four pre-existing assembly-reference-cache warnings were retained, not hidden.
This was a working-tree run based on `7d33fcbafeb27010b775c088ba2a2f3ad888c9e2`
with `sourceDirty=true`, not a claim about that unmodified base commit.

The separate Windows-native adapter fixture also passed actual owned-process
crash/restart, exact committed ciphertext, a 10 MiB receipt, abandoned-temp
recovery, encrypted portable restore/deletion, diagnostic restart and actual
junction rejection with target preservation. No SCM or installed-host qualification
is inferred. Full local regressions passed **1286/1286 across 25 projects**, zero
failed/skipped, from `2026-11-02T04:38:19.8076295Z` through
`2026-11-02T04:42:30.2566621Z`. Changed-source format, native tooling AST guards,
privacy baseline (1293 tracked files), Markdown links (157 files/553 links) and
`git diff --check` passed. All five checks succeeded on final head
`bf9326a7b2eba1c403c3a7c2af2f2cebb1ed5f7c` (Backend41390314211,
Mobile41390314157, Admin41390314113); review completed04:47:58.580086UTC without
findings and final channels were clear. PR297 is independently merged=true/CLOSED.
Nineteen post-merge Confluence full bodies/titles/versions were verified. This
records one local completion, not Jira Done or installed/first-user acceptance.

### Earlier Attempts

An earlier 2026-11-02 actual Windows run built successfully and passed Foundation,
Identity, Profile/Category, Income/Expense, Intake and Receipt/AI, each with actual
initial/crash/restart and checked cleanup. Foundation confirmed live interrupted
domain/outbox recovery. Projections failed at its existing bounded 1000-record
generation-capacity check; its database stopped cleanly. Insights and Audit were
not run after that failure. That earlier matrix remains **failed**, not qualified.
The isolated unchanged 1000-record capacity check then passed initial and restart
at `2026-11-02T04:07:58.4247401Z`, followed by a second pass in the full matrix at
`2026-11-02T04:17:51.1405762Z`. Fixed non-sensitive capacity phase labels were added
for future diagnosis; no production limit, timeout or assertion was weakened.
The original failure was not reproduced and no speculative root-cause fix is claimed.
The earlier sandboxed initialization failure and cold-build timeout remain failed
attempts, not successful coverage. No new completion credit, Jira transition,
merge, installed qualification or first-user acceptance is recorded by this guide.
