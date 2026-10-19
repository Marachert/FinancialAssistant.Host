# Native Receipt, OCR And AI Workflow Persistence

FIN-285 adds service-owned PostgreSQL workflow adapters on the FIN-274 foundation
and FIN-277 [protected encrypted receipt objects](windows-native-adapters.md).
This is not a broker, a WPF installer, installed-Windows acceptance or permission
to enable an external provider. Development and testing retain their existing
in-memory adapters. WindowsNative rejects missing durable configuration, invalid
roles/keys/schema and live AI/OCR enablement; it never falls back to volatile state.

## Ownership And Migration

Receipt Processing uses `fa_receipt_processing` and its matching runtime role;
AI Orchestration uses `fa_ai_orchestration` and its separate matching role. Neither
adapter can use the other's connection or tables. Both use the version-1 owned
record migration, encrypted authenticated payloads, keyed opaque indexes,
revision-checked writes and bounded transactions. There is no extra schema DDL,
automatic migration of an existing deployment, shared business table or superuser
runtime connection. Provisioning and migrations remain separate operator actions.

Both service prefixes require `Storage:Postgres:Database`, `Port`, environment-
provided `Password`, base64 32-byte `IndexKey`, `ActiveEncryptionKeyId`, and
`EncryptionKeys:<id>`. Use `ReceiptProcessing` and `AiOrchestration` respectively.
Do not put values in tracked configuration, PR evidence, Confluence or logs.
Receipt also requires the separate protected file-root settings from FIN-277.
The native host catalog requires the matching PostgreSQL password/index references.
Existing loopback database, schema/grant/key validation and runtime/migration-role
separation remain authoritative; adapters do not relax them.

## Upload And OCR Transactions

Native upload holds the Receipt owner's database lock across the application
operation. The encrypted object is written before metadata; live object identity,
digest and exact size must match before metadata or an idempotent upload replay is
accepted. Metadata, user-scoped idempotency index, unique receipt-owner marker and
the immutable `receipt.uploaded.v1` outbox intent commit together. A competing
request uses the stored original, never a second accepted receipt. Changed content
under the same key conflicts; another owner cannot read that receipt.

PostgreSQL and NTFS are not a distributed transaction. A crash after a file write
but before database commit can leave an **unaccepted encrypted orphan**, subject
to the file root's bounded retention/cleanup. It does not leave accepted volatile
metadata or publish an uncommitted intent. No success is returned before database
commit; a missing/deleted/expired object cannot produce a successful upload replay.
Cross-resource backup consistency and deletion-journal recovery remain FIN-209/286.

Native OCR processing uses the same owner transaction boundary, immutable first
result and atomic completion intent. Metadata preserves exact decimal candidates,
status, confidence, bounded ambiguity/failure codes, provider/model identity,
logical request units and timestamps. Only normalized suggestion fields enter the
completion intent. No receipt bytes, raw OCR text, provider response or arbitrary
provider exception is serialized. This is never a confirmed income/expense.
Zero totals remain valid reviewable OCR suggestions, matching the existing
normalizer; negative totals remain invalid. This does not relax financial-owner
validation or confirm a zero-valued transaction.
Owner locks and revision checks, not process-local semaphores, protect persistence.
Native live OCR is disabled; transaction-scoped processing is bounded by the owned
store deadline and is not a design approval for a long paid provider call in SQL.

## Delivery Is Not Staging

Native publishers implement `ConfirmsDelivery = false`. Enqueue success does not
set `ReceiptUploadedPublished` or `OcrCompletedPublished`. A bounded pending scan
returns at most 25 immutable intents and preserves a continuation cursor. Exact
replay does not add another intent; changed content under an existing event identity
conflicts. Published flags can only be set after the corresponding durable outbox
acknowledgement exists and matches the owner/receipt/event.

The acknowledgement method is an internal transport hook, not a public API or
proof that a broker was contacted. FIN-276 must call it only after confirmed
delivery and wire the consumer/inbox/retry lifecycle. Receipt readiness remains
unhealthy for that missing delivery qualification even when its database is ready.
The development authenticated HTTP completion publisher is unchanged. No native
background worker silently calls that development transport or an external provider.

## AI Metadata, Quotas And Uncertain Attempts

AI owns subject-isolated immutable privacy-safe call metadata, daily usage reservations and a
request journal. A repeated call ID can only retain its exact original metadata;
changed tokens/status/identity conflict. The subject is an authenticated opaque
owned-record index, not an extra raw identity field in call metadata. Another
subject cannot read or attach that call to its journal. Metadata contains safe identifiers,
bounded counts, enum status, timestamps and allowlisted failure categories, never
prompts, request input, structured output or exception details.

Native capability calls require a stable opaque `RequestId` and usage subject.
The request journal commits a reservation before execution, with only an encrypted
SHA-256 input-contract digest and state. A changed request under the same identity
conflicts. A repeated reserved, failed or completed attempt raises
`AiRequestAlreadyRecordedException` instead of invoking a provider again.
Completion links the safe call ID; a crash can leave `reserved`, deliberately
conservative. There is no automatic reset, refund, new request ID or paid retry.

Responses remain transient. A completed result cannot be reconstructed from safe
metadata. The caller must preserve a received suggestion or use the existing
manual-entry/retry-later flow; this implementation does not add a capability HTTP
endpoint or pretend that an uncertain provider outcome succeeded. Administrative
recovery and installed lifecycle qualification remain explicit follow-up gates.

The shared owned usage primitive atomically retains a per-owner/provider/UTC-day
counter and stable request reservation. Concurrent requests cannot exceed the
configured limit; an exact reservation replay does not increment the counter.
Committed AI reservations survive a crash and are not refunded on cancellation
or metadata failure. Native synchronous quota calls fail closed; application calls
use the asynchronous interface. These are logical request bounds, not financial
authorization or a dollar spending cap. Live providers remain disabled regardless.

## Verification And Remaining Gates

The `ReceiptAi` scenario uses only synthetic data and an already verified free
PostgreSQL Windows archive:

```powershell
pwsh -NoProfile -NonInteractive -File tools/scripts/test-owned-postgres.ps1 `
  -Scenario ReceiptAi -ArchivePath <verified-local-postgresql-archive>
```

Build `FinancialAssistant.ReceiptAi.Persistence.Integration` in Release first.
The fixture is constrained to its fresh GUID-owned cluster, validates the real
database directory before provisioning, separates service roles, creates only
its owned protected NTFS root, and invokes an immediate PostgreSQL stop/restart.
It checks metadata/object coordination, independent-scope concurrent replay,
rollback, owner/role isolation, exact decimals, staged versus acknowledged intents,
durable quotas and conservative AI request replay. It never uses production data,
SCM registration, a live broker or a paid provider. Process crash is not power-loss,
backup-system, three-OS, provider-contract or first-user acceptance.

Recorded local verification on 2026-10-19: full solution restore and Release build
passed (zero errors; five pre-existing Analytics/cache warnings), and all
1,046 tests across 24 projects passed with zero skips. The final owned ReceiptAi
fixture passed and confirmed cleanup after immediate PostgreSQL stop/restart,
including actual application orchestration with a synthetic provider: concurrent
stable requests invoked it once, completed/failed requests were not reissued, and
a different subject could neither read nor attach the resulting call. This is
free local evidence, not exact-head CI or installed acceptance.

The zero-total review regression subsequently passed 92 Receipt tests, giving
verified latest results of 1,047/1,047 across the same 24 projects. The repeated
owned PostgreSQL fixture also retained the zero suggestion and its publication
state across an actual restart, with cleanup confirmed at 2026-10-19 20:17:55 UTC.

FIN-276/286 retain integrated delivery, privacy retention/deletion and crash/recovery
qualification; FIN-209 retains complete backup consistency and deletion history.
FIN-278/281 and the owner retain installed Windows/SCM acceptance. There is no
release-ready flag change or extra completed-ticket credit before guarded merge,
Jira closure and the next published POC census.
