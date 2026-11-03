# Native Portable Recovery

FIN-209 is In Progress. This increment provides authenticated portable artifact
encryption and an actual isolated Income/Expense restore drill. It is not a
complete POC backup utility, installed-host qualification or permission to restore
an existing database. No completion credit or first-user readiness is claimed.

Follow the [native deployment decision](../architecture/windows-native-poc.md),
[protected configuration contract](windows-native-protected-configuration.md),
[persistence qualification](native-persistence-qualification.md) and
[owner inventory](native-financial-persistence-scope.md).

## Portable Protection

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

## Repeatable Owned Drill

On Windows, using PowerShell 7, .NET 8 and the locally available PostgreSQL archive
pinned by [the component manifest](../../infra/windows-native/component-manifest.json):

```powershell
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
pwsh -NoProfile -NonInteractive -File tools/scripts/test-owned-postgres.ps1 -Scenario Recovery -ArchivePath <local-pinned-postgresql-archive.zip>
```

The runner verifies the archive digest before use. It creates a user-only owned
GUID directory, starts only a temporary loopback PostgreSQL process on a dynamic
port, verifies `SHOW data_directory`, and uses synthetic credentials and data.
It installs no Windows service, vendor installer, certificate or firewall rule.
Do not run the integration executable directly against a shared cluster.

The explicit Recovery scenario:

1. Initializes separate Income/Expense schemas, migration/runtime roles and keys,
   using the existing actual financial persistence assertions.
2. Waits for the source business process to exit and rejects active source client
   sessions. It dumps each owned schema separately from this trusted synthetic
   source, preserving its migration/version metadata and ownership grants.
3. Encrypts the two dumps and the two index/encryption-key bundles, wraps the
   random recovery key with independently generated operator material, then
   clears the original key and deletes plaintext dumps.
4. Independently reads and verifies the bounded four-artifact catalog, unwraps
   the key, authenticates each artifact and creates a new empty restore database.
   Existing targets are rejected. The fresh target revokes default PUBLIC schema
   access; native ownership checks are not weakened to accept a restore.
5. Runs trusted `pg_restore` with `--exit-on-error --single-transaction`, retaining
   schema ownership and grants. It does not clean, drop or overwrite a store.
6. Proves incorrect recovered keys fail at financial reads; reinstates the actual
   recovered keys and verifies exact original snapshot hashes, EUR/USD totals,
   user isolation, independent SQL role denial and immutable confirmation replay
   without additional outbox records.
7. Performs an actual immediate PostgreSQL crash and restart, then repeats source
   and restored-business checks in new processes, including wrong-key denial.

Both phase proofs must explicitly pass before `businessRecoveryVerified=true`.
The new database is in the same owned temporary cluster and reuses its synthetic
roles. This does not qualify replacement-host role provisioning, real DPAPI secret
export or installer activation. The source bundles contain only fixture keys.
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
scenarios in the full persistence matrix; it does not inflate that matrix.

## Complete POC Recovery Gate

Local verification on 2026-11-03: full Release build passed with zero errors and
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
| All owned databases | Capture all thirteen business owners, including the Summary module, their schema/migration versions, encrypted state, durable outboxes/inboxes, deletion markers and retained decryption/index keys. This drill covers only Income and Expense. |
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
