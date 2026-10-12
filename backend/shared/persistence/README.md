# Service-Owned PostgreSQL Foundation

FIN-274 supplies infrastructure, not completed native domain adapters. Npgsql
8.0.9 targets the existing .NET 8 baseline. Owning adapters select this store through
their explicit native profile; FIN-282 introduces Identity wiring and FIN-283
introduces separate Profile/Category wiring; FIN-275 adds independent native
Income/Expense financial records, transactional wrappers and outbox intents.
Developer in-memory wiring remains
unchanged. Foundation tests are not installed-Windows or first-user acceptance.

## Ownership And Provisioning

Each service has `fa_<service>` schema/runtime role and a separate
`fa_<service>_migration` schema/table owner; hyphens become underscores. Gateway
and MCP have no business database. Only canonical service identities are accepted.
Connections are explicit IPv4 loopback, bounded pools/timeouts, no ambient
transactions, detailed errors, parameter logging or arbitrary connection strings.
Passwords and encryption/index keys come from protected environment/configuration,
never tracked settings or CLI arguments. FIN-208/278 owns protected provisioning.

Provisioning must revoke PUBLIC database/schema privileges, grant each runtime
role only CONNECT, owned-schema USAGE, records SELECT/INSERT/UPDATE/DELETE and
schema_versions/encryption_keys SELECT. Migration and runtime configurations use
their respective role credentials, never the shared synthetic fixture password.
The migration role owns its schema/tables but has no
superuser, database/role creation, replication, bypass-RLS or role membership.
Runtime cannot CREATE or inherit/SET ROLE to the owner. Checks reject foreign
application-schema access, wrong identities, owners or schema versions. Grants
and ownership are revalidated on every transaction/readiness connection.
Schema, table and column ACLs must contain only the expected migration/runtime
principals and owner grantor; PUBLIC/foreign grants and runtime grant options are
rejected even when the current connection still has the expected permissions.
Provisioning and destructive production migrations are not performed here.

## Record And Transaction Contract

The service chooses small validated bucket names and domain serialization. HMAC
indexes hide owner/record identifiers; a distinct stable index key is mandatory.
AES-256-GCM payloads bind service, bucket, hashed owner/id and revision as associated
data. A read-only key manifest rejects changed/lost index or encryption keys at
readiness. Rotation registers additional key markers through the migration role
and retains every existing key; runtime cannot silently change the key set.
At most eight retained encryption keys with lowercase/digit/underscore IDs of
up to 32 characters are accepted. Key removal requires a separate reviewed migration.
A key ID permits reading retained old keys; changing the index key requires
an explicit migration, not silent reindexing. Keys/payloads are never diagnostics.
The maximum plaintext payload is 1 MiB. No raw OCR/provider payload is authorized
by this generic infrastructure. Domain owners define minimized DTOs/retention.

An async callback executes in one READ COMMITTED transaction with a 15-second
total bound and 5-second commands. Acquire all logical lock keys up front, sorted
by derived advisory-lock IDs; locks release at transaction end, never session
scope. Callbacks must not escape the session or do concurrent operations on it.
Each write checks the expected revision; inserts start at revision zero. Domain
and outbox/inbox records can share this transaction without sharing another
service's tables. A failed insert/revision/delete marks the transaction conflicted,
prevents further commands and raises `StorageConflictException` at the boundary,
rolling back every earlier write even if the callback ignored a false write result.
The caller maps that typed conflict or re-reads outside the failed transaction;
there is no automatic callback replay (which could repeat external effects).
Exceptions/cancellation roll back;
commit-outcome uncertainty requires idempotent read/reconciliation by the caller.

FIN-282 adds owner-scoped keyset pages (1..100 records) using opaque HMAC cursors,
not plaintext IDs or offsets. Reads authenticate every payload with its stored
owner/id/revision. `DeletePageItemAsync` constrains a cursor to the requested
bucket/owner and checks its revision, with the same conflict/rollback policy as
ID-based deletion. Cursors never authorize access to another owner or service.

FIN-283 supplies reusable technical `NativePostgresSettings`, a strict versioned
`OwnedRecordCodec`, safe HTTP storage-failure mapping and owned-storage readiness.
Domain DTOs and transaction coordinators remain in their owning services. Native
key decoding clears intermediate byte arrays after `RecordProtection` copies them;
unknown JSON members/versions and missing required fields fail closed. Readiness
does not imply event delivery while FIN-276 is pending.

## Migration, Recovery And Follow-On Work

Version 1 creates owned encrypted records and a checksum/version ledger in one
transaction. Repeated application is idempotent. A PostgreSQL catalog-derived
signature of all owned constraints, including
definitions and validation state, is recorded only at initial creation and checked
on readiness/migration; dropped, weakened, extra or NOT VALID checks cannot be
silently re-baselined by rerunning migration. PostgreSQL-major upgrades require
explicit compatible schema-signature verification under FIN-286/280.
Mismatched/newer versions fail closed, and no down/destructive migration is provided. Future migrations must be
additive, ordered, reviewed and covered by compatible-upgrade tests. Rollback
means retain data and run the compatible previous version, not drop tables.
Back up database plus separately protected encryption/index keys consistently;
FIN-209 owns restore qualification. Lost keys are not recoverable from ciphertext.

FIN-282 owns Identity; FIN-283 separate Profile/Category adapters; FIN-284 Intake
and FIN-275-owned event confirmation boundaries (owner APIs reconcile only);
FIN-285 Receipt/OCR/AI metadata with
FIN-277 encrypted objects; FIN-276 durable event delivery. FIN-286 qualifies the
complete domain migration/retention/concurrency/crash/restart suite. Actual SCM,
installer/package and three-Windows acceptance remain FIN-278/280/281/206.

FIN-275 retains authoritative Income/Expense and matching-owner event consumption;
Intake confirmation stays on its durable outbox, with owner API reads for reconciliation only.
FIN-287 owns separate Summary/Analytics adapters, FIN-288 Score/Recommendations/
Notifications/preferences and FIN-289 append-only Audit storage. This explicit
[original-scope decomposition](../../../docs/engineering/native-financial-persistence-scope.md)
adds unfinished leaf units only; these adapters are not claimed implemented by
the inventory, and FIN-286 must qualify all four siblings.

## Verification

Unit tests cover identities, key separation, encryption binding/tampering,
payload bounds and safe failures. PostgreSQL integration runs against a dedicated
synthetic local cluster, never a discovered production connection. Missing fixture
configuration is an explicit failure in the integration executable, not a skipped
test or mocked database pass. No Docker, WSL, cloud, paid provider or extra spend.

On Windows, first build `FinancialAssistant.Backend.sln` in Release, then run
`pwsh -NoProfile -NonInteractive -File tools/scripts/test-owned-postgres.ps1
-ArchivePath <verified-vendor-zip>`. The script verifies the manifest SHA256 before
extracting selected files into a fresh ignored fixture directory. It starts only
an ephemeral synthetic loopback database, validates its actual data directory,
tests initial/repeated/concurrent migration and isolation/atomicity/revision checks,
crash-stops and restarts that database, then verifies persisted encrypted state.
Cleanup checks executable/PID/start-time ownership before stopping the test process,
removes the temporary password file and restores child test environment variables.
It never registers SCM services or changes OS trust/registry/security policy.
Select `-Scenario Identity` for the Identity adapter checks or
`-Scenario ProfileCategory` for exact preferences/taxonomy, concurrent updates,
atomic category/outbox rollback, erasure/replay, cross-role denial and crash/restart.
Select `-Scenario FinancialRecords` for independent Income/Expense, exact decimal/
currency/origin/revision state, immutable replay after corrections, atomic outbox
failure rollback, CAS/concurrency, bounded pending-safe retention and crash/restart.
See the [native financial contract](../../../docs/engineering/financial-records-native-persistence.md);
broker delivery/inbox qualification remains FIN-276.
Each scenario uses the same verified, fresh, owned fixture and cleanup guards.
Unsigned-package execution blocked by Windows policy must remain Blocked; do not
bypass it. Passing a local database test is not package redistribution qualification.

References: [Npgsql usage](https://www.npgsql.org/doc/basic-usage.html),
[Npgsql 8.0.9](https://www.nuget.org/packages/Npgsql/8.0.9),
[PostgreSQL locks](https://www.postgresql.org/docs/18/explicit-locking.html),
[storage policy](../../../docs/architecture/storage-policy.md).
