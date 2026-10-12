# Native Financial Record Persistence

FIN-275 adds independent WindowsNative Income and Expense adapters to the
[owned PostgreSQL foundation](../../backend/shared/persistence/README.md).
This is code and free synthetic verification, not installed Windows, live broker
or first-user acceptance. The [scope inventory](native-financial-persistence-scope.md)
retains FIN-287/288/289 for the other owners.

## Ownership And Configuration

Each host uses its own `fa_income` or `fa_expense` schema, matching runtime role,
separate migration-role owner and stable protected index/encryption keys.
No business table, role or domain DTO is shared. `Income:Storage:Postgres` and
`Expense:Storage:Postgres` use `NativePostgresSettings` with Database, Port,
Password, IndexKey, ActiveEncryptionKeyId and EncryptionKeys. Environment names
use double underscores; credentials and keys must never appear in commands,
tracked settings or evidence. Provision with the foundation's restricted grants,
run its additive version-1 migration under each migration role, then start the
matching runtime role. No automatic/destructive migration or credential generation
occurs at host startup.

WindowsNative requires this configuration and never falls back to memory.
Developer/Testing registrations and public routes/DTOs are preserved.
Owned-storage failure returns safe 503; deterministic invalid manual input is
validated before database access. Storage readiness stays unhealthy while
FIN-276 live-delivery qualification is pending, even when PostgreSQL itself is ready.

## Records And Atomicity

Encrypted `financial_records` retain exact decimal, currency, owner, transaction,
draft/origin, merchant, date, status, revision and timestamps. HMAC-keyed
`record_owners` enforce immutable transaction ownership across users.
Strict versioned snapshots require every field and reject unknown versions/members,
invalid financial values, mismatched ownership/identity/revision and corrupt state.
Rehydration does not rerun a moving date window or rescale amounts.

Native application/consumer wrappers run all state and publisher operations in
one bounded owned transaction. Logical owner locks serialize mutations; initial
confirmation also locks transaction identity. No HTTP, broker, OCR or LLM operation
runs under SQL locks. A publisher failure rolls back records, ownership markers,
confirmation state and outbox together. CAS rejects stale revisions, corrections
preserve origin/source/confirmation time, archive/restore remain reversible and
idempotent, and active totals never sum unlike currencies. No financial-delete
endpoint or TTL for authoritative/archived records is introduced.

## Confirmation And Outbox

Canonical confirmation remains Intake durable `transaction.confirmed.v1` outbox
and 201/200 confirmation intent, followed asynchronously by matching financial
owner consumption under FIN-276. Owner APIs used by Intake are read-only
status/reconciliation, never a synchronous financial confirmation write or
a duplicate path. FIN-284 must not register in-process native financial stores.

A first matching confirmation stores its immutable original, authoritative record,
minimized creation event and event fingerprint atomically. Exact replay is a no-op;
reusing an event ID for different content or changing the original financial
snapshot is rejected. An alternate event ID for the same original validates
against the immutable original without overwriting corrected/current state or
recreating creation events after published-outbox cleanup. These commit receipts
are storage idempotency guards, not completed broker-inbox/transport qualification.

The encrypted outbox preserves canonical shared-envelope event type, stable event
ID/occurrence, owner hash, causation/correlation and deterministic projection fields.
It excludes merchant, raw user/draft identifiers, input text, receipts, OCR,
prompts, credentials and keys. Pending scanning reads at most ten 100-record pages,
returns at most 25 messages and persists an opaque rotating cursor.
Acknowledgement verifies the exact stored event and time; it does not send anything.
Only acknowledged events older than 30 days are removed, at most 25 scanned records
per maintenance pass. Pending events are never expired. The minute worker performs
retention only, not fake dispatch. Authoritative records, immutable originals and
minimized confirmation fingerprints remain retained until a separately verified
financial/privacy/replay lifecycle policy permits removal; FIN-286 qualifies
integrated lifecycle behavior. Backup/restore must retain separately protected keys
and remains FIN-209 acceptance.

## Free Verification

Build the backend solution in Release, then on Windows run:

```powershell
pwsh -NoProfile -NonInteractive -File tools/scripts/test-owned-postgres.ps1 -Scenario FinancialRecords -ArchivePath <verified-vendor-zip>
```

The script accepts only the manifest's exact PostgreSQL SHA256, creates a fresh
ignored synthetic loopback cluster, validates its actual directory and checks
process ownership before cleanup. It crash-stops/restarts only that fixture.
Tests cover separate roles/keys and SQL denial, two owners/currencies, exact
snapshots, concurrent confirmation/update, CAS, conflicting/alternate replay after
correction, archive/restore, atomic publisher-failure rollback, strict malformed
state, encrypted payloads and pending-safe bounded retention. Restart checks the
complete snapshot hash and replay without duplicate events. Missing fixture
configuration fails explicitly rather than skipping or mutating a discovered DB.

No SCM registration, installer, registry/trust change, production migration,
paid provider/API/runner/credit/fallback or additional spending is performed.
Live delivery and complete integrated recovery remain FIN-276/286; installed
Windows 11 and Server 2022/2025, WPF/package and owner testing remain FIN-278/280/281/206.
