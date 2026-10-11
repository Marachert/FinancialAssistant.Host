# Native Profile And Category Persistence

FIN-283 implementation contract, not a claim of installed release acceptance.
Follow the [owned PostgreSQL foundation](../../backend/shared/persistence/README.md)
and [storage policy](../architecture/storage-policy.md). Profile and Category are
separate business owners, not one shared data model or financial authority.

## Ownership And Data

Profile uses `fa_profile` and its own runtime/migration roles; Category uses
`fa_category` and separate roles/keys. Neither reads the other's tables. The
shared module supplies only technical encryption, transactions, role checks and
keyset operations. Domain DTOs, invariants and service composition stay owned.

Profile snapshots preserve user ID, locale, timezone, currency, privacy/AI
preferences, first weekday, decimal monthly budget, notification/onboarding flags,
created/updated timestamps exactly. A budget preference is not a confirmed balance
or financial transaction. Category snapshots preserve every taxonomy/presentation
key, kind/order, normalized aliases, version and timestamps without re-seeding or
incrementing a version on load. Unknown/corrupt versions fail closed, not defaults.
No identities, credentials, provider payloads, receipts or financial ledger data
are copied into either service. Payloads are minimized encrypted versioned DTOs;
user/record IDs are HMAC indexes, never diagnostic fields.

## Mutation And Delivery

Create-if-missing is atomic and registration replay preserves existing user edits.
Owned user-scoped transaction locks serialize read/modify/write and CAS updates;
callbacks execute once, without automatic replay. Updating one owner cannot change
another. Alias normalization/no-op preserves category version/timestamp and emits
no duplicate intent; a real update commits Category state plus an encrypted owned
outbox intent together. No Profile access to Category tables or broker dependence
inside a SQL transaction. Reliable transport/inbox wiring remains FIN-276; a
pending native intent is never falsely marked published by a development adapter.

Category registration validates missing/null owners before opening native storage.
Alias requests validate normalized correlation IDs (at most 256 characters, no
control characters) in both adapters before any store access. Invalid input returns
400, not an unhandled 500 or a retryable storage 503; REST routes/DTOs are unchanged.

## Configuration, Retention And Acceptance

WindowsNative requires explicit stable PostgreSQL role credentials/index/encryption
keys and fails startup/readiness rather than selecting in-memory fallback. Runtime
does not provision/migrate tables or acquire migration-owner privileges. Developer
and test adapters stay unchanged; public authenticated owner-scoped contracts stay
unchanged. Installer-protected provisioning/recovery remains FIN-208/209/278.

For each owner prefix (`Profile` or `Category`), supply these environment-backed
keys under `<Owner>__Storage__Postgres__`: `Database`, `Port`, `Password`,
`IndexKey`, `ActiveEncryptionKeyId`, and `EncryptionKeys__<key-id>`. Keys are
base64-encoded distinct 32-byte values; role passwords are 32..256 characters.
No credentials or working example secrets belong in tracked configuration.
The runtime connects only to loopback with its canonical owner role and validates
the provisioned schema/key manifest. A missing setting fails startup; unavailable
storage/conflict/internal deadline maps to a generic 503 without database details.
Even healthy SQL remains not-ready until FIN-276 qualifies lifecycle delivery.

Deletion is an authorized service/operator boundary, not a new anonymous/public
endpoint. Keep a durable tombstone so stale registration replay cannot resurrect
erased state; remove owned state and apply bounded retention without deleting live
users. Published event retention is bounded; pending intents are not silently lost.
Compatible migration/old-key recovery must preserve user edits and domain values.

Active preferences/catalogs do not expire. Deletion removes the full profile or
catalog (including aliases), replacing it with a minimal encrypted deletion time
under an HMAC owner index. This marker has no automatic TTL: a qualified maximum
registration-replay/inbox horizon must be established before removing it. Category
outbox metadata contains only owner/category/version/change/time/correlation and
event ID, never alias text or financial values. Pending intents remain retained
until qualified transport/reconciliation in FIN-276; this is not a claim of full
cross-service privacy erasure. Published intents expire after 30 days. Maintenance
deletes at most 25 records per pass using a durable keyset cursor. Pending reads
scan at most ten 100-record pages and return at most 25 distinct event IDs; wrapping
the cursor cannot silently hide the sole pending message. The native retention
worker does not send or acknowledge events. FIN-276 must bind acknowledgement to
confirmed transport; FIN-286 qualifies integrated retention/recovery.

Free synthetic PostgreSQL checks cover two users, concurrent bootstrap/preferences/
aliases, no-op/replay, atomic outbox rollback, corruption, role/owner denial,
retention/deletion and actual crash/restart. Unit/mock checks do not establish
installed-host qualification. No Docker/WSL, paid API/runner/provider, production
migration or SCM registration. Final three-Windows installer/lifecycle acceptance
remains FIN-278/281/206 and owner testing; POC first-user readiness is separate.

After a Release solution build, run on Windows:

```powershell
pwsh -NoProfile -NonInteractive -File tools/scripts/test-owned-postgres.ps1 -ArchivePath <verified-vendor-zip> -Scenario ProfileCategory
```

The fixture provisions only a fresh synthetic loopback cluster with separate
roles/keys. It checks exact serialized domain snapshots across an abrupt database
stop/restart, repeat migrations, concurrent bootstrap/preferences/aliases, owner
and SQL role denial, malformed snapshots, atomic rollback after publisher failure,
deletion markers and pending-safe retention. Actual data-directory and executable/
PID/start-time guards prevent use or cleanup of a discovered foreign database.
Its result separately records cleanup and does not assert installed/release ready.
