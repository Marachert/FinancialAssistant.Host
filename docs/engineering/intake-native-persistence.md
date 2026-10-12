# Native Intake Persistence

FIN-284 selects Intake-owned PostgreSQL draft, idempotency, source-reference,
confirmation-intent and outbox storage in WindowsNative. This builds on the
[owned foundation](../../backend/shared/persistence/README.md) and completed
[Income/Expense owners](financial-records-native-persistence.md), not a new ledger.
Developer/Testing stores and public DTOs/routes remain unchanged.
The [canonical flow](../architecture/financial-core-e2e-flow.md) is authoritative.

## Native Configuration

Use separate `fa_transaction_intake` runtime schema/role and its migration-role
owner, with stable protected password, HMAC index key and retained encryption keys.
`TransactionIntake:Storage:Postgres` uses `NativePostgresSettings`; the native
service catalog explicitly requires its Password and IndexKey alongside existing
gateway/receipt-event secrets. Missing/invalid configuration fails startup instead
of selecting memory. Provision grants and apply the foundation migration under
the migration role before starting the runtime role. No schema, credential,
SCM, registry, trust, firewall or production migration is automatically created.

`TransactionIntake:Storage:SourcePayloadRetentionHours` is mandatory and must be
an integer from 1 through 24. There is no silent default or fabricated production
privacy approval. The owner/operator must choose and protect this setting during
FIN-208/278 provisioning and qualify lifecycle behavior under FIN-286.
Environment variables use double underscores; never place credentials, keys,
source input or production configuration in arguments or tracked files.

## Owned State And Transactions

Strict versioned snapshots preserve draft identity, exact nullable decimals,
currency/category/merchant/date, confidence/ambiguities/suggestion provenance,
review requirement, note/source, creation time, status and revision.
Every persisted field is required; malformed/unknown state, mismatched owner/ID/
revision, incomplete confirmation or low-confidence review bypass fails closed.
Rehydration does not rerun parsing, change source authority or use a moving date window.

HMAC-keyed `draft_keys` point to current `drafts` without copying raw input.
Fingerprints are immutable and mismatched input returns the existing conflict.
Global owned identity markers prevent another owner from reusing generated IDs.
CAS and owner-scoped logical locks serialize review, rejection and confirmation;
a stale update cannot be silently reapplied. Terminal drafts are not editable.

Parsing awaits outside SQL. Only deterministic owned mutations enter the bounded
transaction scope: draft/index/creation metadata/source/outbox together, or
confirmation claim/result/state/outbox together. Review/OCR mutations remain pure
local validation and owned storage. No parser/provider, broker or foreign-service
HTTP call runs under SQL locks. A publisher/cancellation/storage failure before
commit rolls back all owned writes, including identity and source markers.
Bad input, note and correlation metadata validates before native SQL access;
invalid merchant control characters become an explicit review ambiguity.
OCR event/user/receipt identifiers must be canonical, bounded and control-free;
malformed authenticated events return `400 invalid_ocr_completed_event` before SQL.
Storage conflicts/unavailability use the shared generic 503 boundary.

## Confirmation And Delivery

Native Intake does not register Income/Expense repositories or consumers.
A 201 first/200 replay confirmation response proves only durable intent with a
stable transaction ID. Intake commits `transaction.confirmed.v1` to its own
outbox; FIN-276 asynchronously delivers to the matching financial owner.
There is no synchronous confirmation write RPC, duplicate path or downstream
rollback of committed Intake state. Any financial-owner API used for reconciliation
is read-only and authenticated. A response, draft or outbox is not financial truth.

Native creation and confirmation publishers enqueue durable intent only.
The legacy internal `Stored...Published` boolean maps to verified local
`Enqueued` state so the existing application does not enqueue again; it is not a
broker acknowledgement. Only the separate outbox `PublishedAtUtc` records verified
transport acknowledgement. No maintenance worker publishes or pretends to send.

Draft-created intent carries identifiers/references/time, never source text,
amount, merchant, note or idempotency material. Confirmation preserves the existing
private financial-owner DTO, including approved deterministic values and bounded
merchant; it contains no intake text, raw OCR, prompts, notes, credentials or keys.
FIN-276 must preserve that restricted producer/consumer boundary rather than
broadcasting financial payloads to operational/Audit channels.

Pending scanning is bounded to ten 100-record pages and 25 returned events, using
a persisted rotating opaque cursor. Acknowledgement validates the stored event
and time. Cleanup scans at most 25 records per pass and removes only acknowledged
outbox rows older than 30 days, never pending events. Stable confirmation and
enqueue metadata survive outbox cleanup, so replay cannot recreate a sent intent.
Native readiness remains unhealthy until FIN-276 reliable-delivery qualification.

## Source Retention And Privacy

Normalized text is encrypted in a separate `source_payloads` bucket behind an
opaque reference; it is never automatically copied into drafts, indexes, events,
logs or evidence. A separately supplied, bounded review note remains user-owned
draft data, not a copy of the source payload.
Reads are owner-scoped and fail at the effective expiry. Stored expiry is immutable:
raising configuration cannot extend an old payload, and a shorter setting clamps
its access cutoff. The bounded maintenance worker physically deletes expired source
rows with an opaque scan cursor, at most 25 scanned rows per pass.
Access expiry is immediate; cleanup throughput and installed lifecycle/privacy
acceptance remain FIN-286, not a promised exact physical-erasure SLA under load.

Replaying an old intake key validates fingerprints but never restores erased
text or resets its expiry. Source lookup returns unavailable after expiry;
downstream orchestration must report that explicitly, not fabricate provider output.
Protected source-fetch/provider workflow integration remains FIN-285/276.
No anonymous or general raw-input endpoint is introduced.

Minimal draft/idempotency/reference/confirmation metadata remains retained until
a separately qualified financial/privacy/replay lifecycle permits removal.
No new public draft-expiry enum or financial-delete endpoint is invented here.
Confirmed references are not another authoritative ledger. Cross-service erasure,
migration/retention capacity and backup/key restore remain FIN-286/209 acceptance.

## Free Verification

Build the backend solution in Release, then on Windows:

```powershell
pwsh -NoProfile -NonInteractive -File tools/scripts/test-owned-postgres.ps1 -Scenario Intake -ArchivePath <verified-vendor-zip>
```

The script validates the exact manifest SHA256, provisions only a fresh synthetic
loopback cluster, verifies its actual directory, crash-stops/restarts the owned
process and checks PID/executable/start-time ownership before cleanup.
Tests cover exact snapshot hashes/indexes, two owners/currencies, concurrent
create/review/confirm/reject, stale CAS and immutable conflicts, atomic
draft/source/outbox and confirmation rollback, parsing outside SQL, explicit
source cutoff/physical cleanup/no replay resurrection, bounded pending-safe
outbox retention, malformed state, separate-role SQL denial and encrypted payloads.
A separate test driver applies the committed event to completed FIN-275 owner
adapters repeatedly and after restart, proving no duplicate authoritative commit;
this is not live RabbitMQ or distributed delivery qualification.
Missing fixture configuration fails explicitly rather than skipping or connecting
to a discovered database.

No Docker/WSL, paid provider/API/runner/credit/trial/fallback, real financial data,
SCM installation or additional spending occurs. Installed Windows 11/Server
2022/2025, WPF/package/recovery and first-user acceptance remain FIN-278/280/281/206.
