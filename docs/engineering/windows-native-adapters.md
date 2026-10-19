# Windows-Native Storage And Diagnostics Adapters

FIN-277 provides encrypted receipt files, disposable service-health caching and a
bounded private technical diagnostic sink. These are native adapter implementations,
not a WPF installer, an accepted Windows deployment or complete receipt workflow.
The [native decision](../architecture/windows-native-poc.md) and
[component inventory](../../infra/windows-native/README.md) retain every required
collector, broker, database, index, signing and installed-target gate.

## Protected File Boundary

`FinancialAssistant.Shared.NativeFiles` accepts only a pre-provisioned local NTFS
directory outside application binaries, including resolved physical/8.3 paths.
It rejects UNC/device paths, alternate streams, traversal aliases and reparse
points. Directory handles pin every ancestor without delete sharing; file handles
reject hard links, reparse points and unexpected entry types. Only fixed opaque
object/temporary names are accepted, never filenames supplied by a client.

The configured service SID must match the running identity. The root must have
protected ACLs owned by that service, SYSTEM or Administrators, with no access
granted to other principals and inheritable service Modify access. Files inherit
that restricted policy; broad or invalid file ACLs fail closed. An exclusive
no-follow writer lease prevents a second process from sharing the same data root.
The adapter validates existing permissions; it does not elevate, repair ACLs,
create service accounts or change machine policy. FIN-208/278 own provisioning.

Receipt writes use encrypted temporary content, file flush-to-disk and same-volume
moves; technical diagnostic batches use the same protected atomic file boundary.
Cancellation before publication leaves no accepted partial object; recovery removes
abandoned temporary files. Directory-entry ordering, disk/controller failure and
power-loss durability are not certified by a process-crash fixture. There is no
automatic import of an unknown directory or recursive production deletion.

The implementation follows Microsoft's [CreateFile sharing/reparse rules](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew),
[handle metadata](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfileinformationbyhandle)
and [final-path resolution](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew).

## Receipt Object Contract

`ReceiptProcessing:Storage:Files` requires `RootPath`, `ServiceSid`, protected
base64 `IndexKey`, `ActiveEncryptionKeyId`, `EncryptionKeys:<id>` and explicit
`RetentionHours` from 1 through 24. Keys are distinct stable 32-byte values; up to
eight encryption keys can be retained for rotation. No key, SID credential or
production setting belongs in arguments, documentation examples or tracked files.
The native service catalog also explicitly requires the index secret.

AES-256-GCM authenticates the versioned receipt identity, original size/digest,
creation/expiry, deletion state and image bytes, with the header and opaque HMAC
filename bound as associated data. Objects are bounded to 10 MiB. Persistent key
authenticated markers detect a wrong index key, changed historical encryption key or missing
retained key instead of treating unreadable state as empty. New keys can be added;
removing history requires a separately qualified migration/restore policy.

Store replay is exact and idempotent, never an overwrite with different bytes.
Deletion and expiry replace ciphertext content with a minimal encrypted tombstone.
The original digest/identity and deletion marker remain to reject resurrection;
they are not another financial ledger. Reads enforce expiry before returning a
zero-on-dispose plaintext stream. Application callers must first authorize the
receipt's user through its service-owned metadata; no raw-content HTTP route is
introduced. Receipt bytes are never used as cache or diagnostic data.

The root remembers the minimum retention ever configured. Lowering the duration
is durable; raising configuration on that root cannot re-expose old input or
silently lengthen new objects. A later policy epoch/root and migration require
explicit operator/privacy qualification. There is no default retention or invented
production privacy approval. `MaximumObjects` defaults to 1000 and is bounded to
1..9968, including tombstones. Capacity exhaustion fails closed, not by deleting
replay markers. Minute maintenance scans at most 25 entries per pass; physical
cleanup capacity is not an exact erasure SLA under load.

Encrypted export/restore preserves original metadata and expiry, authenticates the
target identity/key ring and cannot override an existing tombstone or effective
expiry. Export carries a shortened root cutoff into the encrypted object rather
than restoring a longer old policy on another root. A fresh restore from an old backup still needs the latest deletion journal,
matching service metadata, protected keys and a consistent recovery point. FIN-209/
286 qualify that system-level lifecycle; an isolated object restore is not proof
of cross-service deletion, full backup recovery or key escrow.

Native Receipt startup validates its object configuration and root without memory
fallback. FIN-285 supplies [owned Receipt/OCR workflow metadata](receipt-ai-native-persistence.md);
unavailable/invalid storage returns safe `503 receipt_storage_unavailable`, with no-store and
no raw path/exception details. Delivery readiness remains unhealthy pending FIN-276.
Persisting files alone never acknowledges an upload backed by volatile metadata.
Developer/Testing adapters and public routes/DTOs remain compatible; paid OCR stays
disabled in WindowsNative.

## Disposable Health Cache

`Monitoring:ServiceProbeCacheSeconds` defaults to zero (disabled); values 1..5
enable a maximum of 32 process-local service-health observations. The original
checked-at time is preserved, expiry is monotonic, old in-flight probes cannot
replace newer observations, and restart discards the cache. RabbitMQ/Elasticsearch
aggregate probes remain fresh. Cached display observations never authorize access,
confirm a transaction, enforce quota or own session/idempotency/financial state.
This is not a distributed cache or an event-delivery store; no Redis port is needed.

## Private Local Diagnostics

Each native host can enable `NativeDiagnostics:Enabled` with a separate protected
`RootPath`, matching `ServiceSid`, explicit 1..24-hour `RetentionHours` and 4..128
`MaximumBatches` (default 32). Disabling/unconfiguring the native sink does not stop
financial HTTP handling, but readiness reports the visibility gap. Console logging,
Monitoring/Audit/MCP services, authenticated admin APIs and existing trust boundaries
remain intact. No new public diagnostics endpoint or cloud exporter is enabled.

The local provider never invokes a message formatter or reads arbitrary log state,
exception, category, event name or scope. Its fixed record contains UTC time, numeric
event ID, severity and optional W3C trace/span IDs, plus a controlled service ID in
the batch. It cannot serialize financial values, identities, receipts, OCR or prompts.
The process queue holds 256 records; batches contain at most 25 and reads at most
100 recent records. Queue overflow and storage loss have explicit counters and
unhealthy readiness, not fake successful telemetry. Corrupt storage backs off;
a completed writer terminates on persistent failure instead of spinning. The host
shutdown deadline still applies. Strict schema and service checks
reject unknown/cross-service files. Stored expiry is immutable or shortened, never
extended by raising configuration; expired and excess old batches are rotated on
startup/write maintenance. This is disposable technical evidence, not append-only
Audit authority or a promised idle-time physical-erasure SLA.

The native Prometheus/Alertmanager/Grafana/Jaeger candidates and retained
Elasticsearch contracts are still required where the manifest says so. This sink
does not claim their dashboards, complete distributed tracing, alert evaluation,
collector privacy filters or approved deployment. FIN-216/280/281 retain that
qualification; FIN-289 owns durable Audit metadata. No paid exporter or provider is
activated and no required capability is silently removed.

## Free Verification

After a Release solution build, on Windows:

```powershell
pwsh -NoProfile -NonInteractive -File tools/scripts/test-native-adapters.ps1
```

The fixture uses only fresh GUID-owned synthetic temporary directories. It verifies
the actual child PID/start-time/executable before force-stopping that child alone,
then checks exact committed ciphertext, a 10 MiB object, abandoned-temp cleanup,
encrypted portable restore/deletion and diagnostic restart. A real owned NTFS
junction is rejected while its protected target is preserved. Cleanup verifies
exact temporary paths and removes the junction itself before its owned directory.
No existing service, database, process tree or installed configuration is touched.

Windows tests exercise ACLs, hard-link/path denial, one-writer ownership, replay,
key rotation, expiry policy rollback, cancellation/capacity, native safe 503,
diagnostic canaries, drop visibility and restart/rotation. On non-Windows test hosts,
native-only cases assert platform rejection; they do not certify Windows behavior.
Installed Windows 11/Server 2022/2025, least-privilege identities, actual recovery/
backup, collector acceptance and first-user Go remain FIN-278/281/209/216/206.
