# Windows Native Protected Configuration

Related scope: FIN-208; integration FIN-278/279; recovery FIN-209. This is an
implementation contract, not evidence that the owner machine has been installed
or that FIN-208 is Done. The Windows installer, first-admin bootstrap, coordinated
credential rotation and approved-host qualification remain pending.

## Runtime Boundary

All fifteen executable hosts use `FinancialAssistantHost.CreateBuilder`. Under
Windows SCM, `WindowsNative` is mandatory and the protected configuration source
is mandatory before native validation or business dependency registration.
There is no environment-variable, command-line, user-secrets or development JSON
fallback for installed runtime secrets. Missing, foreign, malformed or undecryptable
artifacts stop startup with sanitized errors. Paid AI, OCR and phone providers
are always disabled in this installed POC profile.

The packaged `appsettings.json` supplies non-secret code-owned defaults such as
the gateway route catalog. It is read once, not watched. Non-empty cataloged runtime
secrets in that file are rejected. Previous configuration providers are discarded;
only this baseline and the validated protected snapshot remain. Baseline defaults
are not operator secret storage. Do not place secrets in any JSON release artifact.

An interactive console `WindowsNative` launch retains the earlier explicit
developer/owned-fixture bootstrap behavior. It is **not installed acceptance** and
must not be presented as a release secret mechanism. Do not expose that console
path, log its configuration, or copy fixture credentials into a release.

## Artifacts And Access

The production reader has no operator-selectable root or cryptographic provider:

```text
%ProgramData%\FinancialAssistant\Configuration\<canonical-service-id>\
  installation.id
  current.json
  <32-lowercase-hex-reference>.secret
```

`installation.id` is exactly the installation GUID in `N` format (32 ASCII bytes,
no newline). `current.json` is the bounded, non-secret revision document.
Each `.secret` is a machine-DPAPI blob with an opaque GUID name, not a plaintext
password or connection string. Do not copy this directory into support bundles.

The reader is read-only: it never creates directories, changes ACLs, repairs
permissions, installs services or generates missing credentials. FIN-278 must
provision the owned layout before services start. Requirements:

- Local fixed NTFS only; actual `NT SERVICE\FinancialAssistant.<id>` token and
  translated service SID must match the document. An interactive administrator or
  LocalSystem process cannot substitute for the intended virtual service identity.
- Directory and every file: protected DACL, owner System or Administrators,
  explicit System and Administrators FullControl, only the intended service Read
  (ReadAndExecute is allowed), no service write/delete/ACL/ownership grants,
  additional principals, inherited entries, deny rules or inherit-only entries.
- Parent traversal must permit the service to open/list the pinned directory
  chain. A parent-only traversal grant is insufficient for the reader's handle
  pinning policy. Do not grant broad access to individual service artifacts.
- Every path ancestor is opened without following reparse points. Real directory
  list access and no delete sharing prevent rename while reading; metadata-only
  directory access does not establish this guarantee. File handles prohibit
  concurrent writes/deletion and reject hardlinks, reparse points and directories.
- ACLs are read from those same handles, not from an unchecked replacement path.
  Root final path, installation marker, intended identity and owned references
  are checked before any secret decryption.

`current.json` is at most 64 KiB, depth 16, schema version 1, at most 96 literal
settings and 64 secret references. Unknown fields, case-folded duplicate JSON
properties/setting names, duplicate reference IDs, cross-service/installer keys,
wrong JSON types and missing required fields are invalid. The parser takes an
immutable snapshot; mutating an operator DTO or exported byte array cannot change it.

## Secret Inputs

`NativeConfigurationPackage.Create` accepts in-memory byte arrays from a trusted
installer caller. It validates the exact owned secret set, relationships and
key sizes, then returns ciphertext plus a non-secret plan. Its API does not save
files or authorize activation. The caller owns and must clear its input buffers.
Never supply these values as command-line arguments, environment-based release
configuration, wizard journals, logs or support metadata.

Runtime references cover the existing cataloged JWT/hop/Identity keys, owned
PostgreSQL runtime passwords and index/encryption keys, seven current native
broker connections, Profile's three distinct financial-reader credentials and
the receipt-file index/encryption keys. Migration/admin/bootstrap credentials
are not runtime keys and must not be put in the service-readable directory.
Optional paid-provider credentials have no accepted configuration entry here.
Summary is a separate module without an assigned executable owner; this contract
does not invent a new Summary service or silently claim its installer integration.

The package requires the PostgreSQL and bounded retained key set for each current
database host; enables the seven existing broker workers and Profile financial
readers; selects Identity's native adapter and pending-transport outbox mode; and
requires receipt storage/diagnostics paths and retention. Missing secret material
never selects an in-memory replacement. A runtime password/connection string is
still validated by its existing business adapter after loading.

Machine DPAPI uses `System.Security.Cryptography.ProtectedData` 8.0.0 (Microsoft,
MIT), `LocalMachine`, with 8 KiB plaintext/16 KiB ciphertext bounds. Purpose entropy
binds installation GUID, service ID, service SID, configuration key and opaque
reference GUID. A replay under different metadata fails. This binding is not an
authorization credential: anyone able to read a machine blob and its metadata
could invoke machine DPAPI. **Filesystem ACLs and actual service identity are
mandatory**, not optional encryption hardening.

Temporary byte buffers are cleared on success/failure. .NET configuration contains
managed strings needed by existing adapters; those cannot be reliably zeroed.
Never log configuration enumerations, `GetDebugView`, memory dumps or raw
exceptions containing credentials. Secret-bearing APIs have privacy-safe
`ToString` output; that is not permission to serialize runtime configuration.

## Activation And Rotation

FIN-278's privileged engine must own actual disk activation. Required integration
sequence, **not an implemented actuator in this component**:

1. Verify installation ownership, elevated writer authorization, free space and
   the protected root; acquire the exclusive bounded installer lock.
2. Validate the entire new plan and secret package in memory. For an update,
   compare the freshly read active revision and invoke
   `NativeConfigurationRotationPlan.Create(previous, next)` before writing.
3. Create new secret-reference files without overwriting existing immutable
   references. Apply/verify the required protected descriptor before writing data;
   flush ciphertext. Reused reference IDs must reuse existing immutable blobs.
4. Prepare and flush a same-volume temporary non-secret document with its final
   descriptor, then atomically replace `current.json` only after every referenced
   artifact is durable. Recheck the expected prior revision under the lock.
5. Coordinate any database/broker/JWT/hop peer change and controlled service
   restart; verify readiness. Journal only opaque operation/revision IDs and
   sanitized outcomes. Crash/compensation must retain the last usable revision
   and needed key material; do not delete unknown or foreign files.

The pure rotation plan does not rotate live credentials or claim atomic
multi-service activation. It rejects installation/service/SID changes, same
revision reuse, omitted old references, changed index/HMAC/retained encryption
references, and unverified database/storage-identity relocation. A new encryption
key may be appended and selected while old keys are retained (maximum eight).
Credential-reference changes are explicitly reported and require coordinated
activation plus a controlled restart; same GUID is not permission to overwrite a
blob with different material. Retirement/reindexing requires verified migration
and recovery evidence, not automatic key regeneration.

First-admin creation must be an explicit one-time authorized Identity operation
with durable replay protection and no shipped default password or public role
promotion. This operation and the WPF secure-input bridge are not implemented by
the runtime store. The mobile client must receive only the validated gateway HTTPS
public origin and public trust instructions, never server/installation credentials
or certificate private keys. A local-only origin is not proof of phone reachability.

## Recovery And Verification

Local component verification on 2026-11-02: full backend 1,445/1,445 tests across
25 projects, zero failures/skips; hosting/configuration 203/203. Full Release build
passed with existing cache warnings; final scoped build had zero warnings/errors.
The owned native adapter runner also passed actual receipt/diagnostic crash/restart,
encrypted restore and junction rejection, with new root/ancestor rename assertions.
These are free local results, not exact-head GitHub CI or installed qualification.

Machine DPAPI is machine-bound and **not a portable backup**. FIN-209 must provide
operator-controlled encrypted recovery of required data/HMAC keys alongside a
consistent database/file backup, verified on an isolated replacement host. Copying
only ProgramData blobs cannot satisfy disaster recovery; do not attempt a
destructive production restore or invent success from serialization tests.

Free tests cover all fifteen typed profiles, encrypted package load, actual Windows
DPAPI replay/tamper rejection, ownership-before-decryption, provider-fallback
removal, descriptor-policy negatives, real owned file/directory rename/write
protection, hardlink rejection and conservative rotation rules. They use synthetic
values only and do not install services, modify trust stores or contact providers.
Non-Windows runs exercise portable policy/negative paths, not Windows filesystem
or DPAPI acceptance. Actual intended-service positive ACL reads, privileged atomic
activation, multi-service rotation/crash recovery, admin bootstrap and installer
lifecycle remain pending until integrated and qualified on an approved Windows host.

References: [Microsoft machine DPAPI](https://learn.microsoft.com/en-us/windows/win32/api/dpapi/nf-dpapi-cryptprotectdata),
[handle security descriptors](https://learn.microsoft.com/en-us/windows/win32/api/aclapi/nf-aclapi-getsecurityinfo),
[file handle sharing/reparse behavior](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew),
[HTTPS perimeter](windows-native-https.md),
[native installer decision](../architecture/windows-native-poc.md).
