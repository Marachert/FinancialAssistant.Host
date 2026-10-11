# Native Identity Persistence

FIN-282 implementation contract. This document describes the scoped increment;
it describes the code in this change, not already delivered installed Windows
acceptance. Follow the [PostgreSQL foundation](../../backend/shared/persistence/README.md)
and [storage policy](../architecture/storage-policy.md).

## Ownership And Representation

Identity alone owns `fa_identity`, runtime role `fa_identity` and separate
`fa_identity_migration` owner. Profile, Category and financial services never
read these tables. Public API authorization remains mandatory; loopback and a
service database role are not user authorization. Account IDs/status/roles/time,
credential hashes/algorithms, provider HMAC links, session metadata, phone workflow
and rate ledgers use validated, versioned DTOs in encrypted owned records.
No raw email/phone/provider subject, password, refresh/access token or OTP is stored.

Account rehydration preserves its immutable identity and permission state through
a validated factory, never a private-constructor JSON bypass. Lookup indexes use
existing purpose-separated hashes and foundation HMAC indexes. Logical buckets
and key construction distinguish account IDs, email lookup hashes, provider links,
session IDs/families, phone/client state and outbox IDs without delimiter ambiguity.
Corrupt/unknown DTO versions fail closed without echoing sensitive values.

## Atomicity And Concurrency

Native Identity mutations use one bounded database advisory lock for the POC.
This intentionally favors a clear cross-process invariant over premature write
partitioning; it is confined to Identity, not a global financial authority.
The operation coordinator keeps account/credential/link, initial session and
outbox intents in the same SQL transaction. Responses are created before commit
but returned to the endpoint only after commit; an outbox failure cannot leave
an account or a rotated session committed. There is no automatic replay of a
callback or external provider operation after an uncertain commit.

Store methods remain asynchronous. The existing initial-session issuer's blocking
async bridge is now async end-to-end so PostgreSQL I/O and cancellation do
not block request threads. Developer/testing adapters retain their existing behavior.
Provider validation/delivery is not a financial transaction; paid phone/AI/OCR
providers remain disabled, and no live provider acceptance is inferred.

Sessions are individual records owned by a token-family key, with encrypted
session locators. Family revocation has a durable tombstone, so refresh/reuse,
logout and initial creation cannot race across processes or require an unbounded
serialized family document. Hash comparison stays fixed-time; mismatched account,
family or client identity never gains another user's session. Expired/revoked
states remain distinct and retention preserves replay detection windows.

Phone reservation updates its challenge, active-phone pointer and phone/client
window ledgers atomically. Pending-dispatch, cooldown, expiry, lockout and one-time
completion survive restart. Pruning is bounded to relevant windows and owned
records; raw delivery codes are never persisted. Disabled delivery is unavailable,
not a fabricated successful challenge.

## Outbox, Readiness And Recovery

Outbox envelopes and retry/publication state are encrypted and share the operation
coordinator. Stable event IDs remain unique and retries do not recreate intents.
No native intent may be marked published by an in-memory transport. Reliable
RabbitMQ delivery/consumer inbox qualification remains FIN-276; Identity currently
has no implemented inbound lifecycle consumer to claim inbox acceptance for.

WindowsNative requires explicit PostgreSQL role credentials and stable, separate
index/encryption keys. Missing/invalid configuration fails startup; unavailable,
wrong-role/schema/key or corrupt storage fails readiness. No native fallback to
process-local state. Runtime has no migration CREATE privileges. Installer-owned
provisioning/migration and protected key recovery remain FIN-208/278/209.

Fresh/repeated migration is non-destructive and version checked. Bounded keyset
reads support owned retention/deletion work, never arbitrary SQL or another owner.
Each cleanup transaction scans at most 25 records; a deletion transaction scans
one job plus at most 25 related records, persisting opaque keyset cursors. A native background worker processes one cleanup page and one
deletion-job page per minute; it never creates deletion requests. The authorized
operator/application deletion boundary must call `DeleteAccountBatchAsync` to
create an encrypted job and immediately set `DeletionPending`; there is no new
public deletion endpoint in this increment. Jobs resume after restart, retain
removed credentials/links for the configured window, then remove account-owned
credentials/links/sessions and mark `Deleted`. The deleted account and completed
job are removed only after the configured deleted-account retention.

Session records/family replay tombstones remain until refresh expiry plus terminal
retention, even when an earlier session is rotated/revoked. Expired phone challenges
follow terminal retention; phone/client rate ledgers survive at least the maximum
one-day start window. Published outbox rows expire after 30 days; pending intents
never expire silently. Explicit account deletion disables authentication before
its cleanup and may delete that account's otherwise-active sessions. Ordinary
retention never deletes active accounts/non-expired sessions. Back up database plus protected
keys consistently. Rollback preserves records and uses a compatible code version;
no production conversion or destructive down migration is authorized here.

## Evidence Gate

Use synthetic data and a dedicated, owned, no-cost PostgreSQL fixture for concurrent
registration/federation, duplicate hashes, atomic state/outbox rollback, refresh
rotation/reuse/family revocation, wrong-owner requests, phone transitions/windows,
retention, migration and actual process/database restart. Unit doubles do not
replace database evidence. Existing Identity/API/privacy tests remain mandatory.
Record exact-head CI and reviewed merge plus Jira/Confluence/percentage closure.
Actual Windows SCM/installer matrix remains FIN-278/281 and owner final testing.

## Native Configuration And Commands

Set `Identity:Storage:Provider=PostgreSQL`,
`Identity:Authentication:RuntimeAdapter=NativePostgres` and
`Identity:Events:Mode=DurableOutboxPendingTransport` in protected native configuration.
Native broker readiness deliberately fails until FIN-276; no fake transport runs.
Existing lookup, refresh, signing, provider-identifier and event-subject keys must
be stable and at least 32 characters. Never use synthetic keys outside tests.

`Identity:Storage:Postgres` requires `Database`, `Port` (explicit IPv4 loopback),
`Password`, `IndexKey` (base64 32-byte key), `ActiveEncryptionKeyId` and
`EncryptionKeys:<id>` (base64 32-byte keys). Index/encryption keys must be distinct;
keep every registered key for decryption. Runtime credentials use `fa_identity`,
not the migration owner. Migrations are run separately with the migration role's
credential by installer-owned provisioning; the API never creates tables/roles.
Maximum native access lifetime is 60 minutes and refresh lifetime 90 days.
Phone delivery stays Disabled. No values belong in Git or command-line arguments.

Build the full backend solution in Release, then on Windows run:

```powershell
pwsh -NoProfile -NonInteractive -File tools/scripts/test-owned-postgres.ps1 -ArchivePath <verified-vendor-zip> -Scenario Identity
```

The existing hash-pinned free fixture validates the actual fresh data directory,
uses synthetic keys/identities, executes both initial and crash/restart stages,
and stops only owned PostgreSQL processes in `finally`. `Foundation` remains the
default separate foundation scenario. Neither scenario installs SCM services,
downloads packages, calls paid providers or certifies the three-OS install matrix.
