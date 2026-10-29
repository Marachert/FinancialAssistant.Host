# Native Audit Persistence

FIN-289 is locally Verified Merged, with Jira reconciliation pending. Isolated PostgreSQL
verification passed; no installed Windows, live transport, full privacy
recovery or first-user acceptance is claimed. Jira is suspended; use the
[owner-approved local backlog](../agent/LOCAL_BACKLOG.md), without paid access or
fabricated transitions. Local fixed-scope completion is195/215 (90.7%), P9 19/39
(48.7%), FIN-27013/19 and open. The separate last live Jira baseline is193/215;
this is not a fresh census or first-user acceptance.

[PR293](https://github.com/Marachert/FinancialAssistant.Host/pull/293) was
independently verified merged=true/CLOSED: actual squash
`f870733b35bc1d74a4248f882d4426faeef1e92e` at `2026-10-29T13:53:43Z`, final head
`77adda433b0e8f3d58c6495a50bc34617615326d`. All five checks passed
(Backend40883169464, Mobile40883169618, Admin40883169414). Both valid findings were
fixed, regression-tested, answered with evidence and resolved only after green;
re-review completed13:36:39.395023UTC with no findings. Nine affected post-merge
Confluence full bodies were independently read back.

## Ownership And Atomic Append

Audit uses its own `audit` identity, `fa_audit` runtime/schema and
`fa_audit_migration` role, stable index/encryption keys and versioned owned DTOs.
Native configuration requires `Audit:Storage:Postgres`; no memory or legacy
RabbitMQ-consumer fallback is registered. Readiness remains unqualified for
FIN-276 delivery even when the database is available. Unavailable native HTTP
operations fail safely with no-store; no credentials or database details enter
responses. Developer/test in-memory contracts remain compatible.

An optional application transaction wraps complete source handling. A native
cross-process Audit lock atomically commits the immutable entry, correlation index
and minimized replay receipt. Missing append, conflicting replay or SQL failure
cannot commit partial success. Source fingerprints cover the entire bounded
envelope, including occurrence, actor/subject, correlation, causation and retention
classification. Identical replay returns its original deterministic Audit ID and
never refreshes recorded time or expiry. Expired replay never recreates a payload.

Owned required-field DTOs preserve only allowlisted technical metadata. Audit is
not financial authority and stores no amount, note, raw identity, receipt, OCR,
prompt/response, credential or arbitrary event payload. Legacy v1 events without
actor metadata preserve the existing compatibility policy; explicit actor events
retain catalog tuple and pseudonymous actor-hash enforcement. No public update,
delete, bulk export or unrestricted user-search endpoint is introduced.

Current producer and retention-class admission applies to incoming events, not
previously accepted evidence. Historical decoding still enforces safe fields,
catalog tuples, actor hashes, canonical metadata and immutable timestamps without
reapplying the current ingestion allowlist. Revoking a producer or removing a
configured class cannot block historical reads or cleanup; durable class minima
remain authoritative. Revoked producers cannot submit even an identical replay.

## Queries And Retention

The existing authorized admin API still looks up one correlation ID. Native
storage uses 100-row index pages and at most 1000 indexed entries per correlation;
the next append fails atomically rather than evicting evidence or truncating a
response. Expired entries are never returned. Cleanup may be needed before stale
index entries free correlation capacity. This is an explicit POC limit, not
unbounded production history or a high-throughput qualification.

Retention classes retain explicit 1..3650-day policy bounds. Stored event and
first expiry times are immutable. A durable lifecycle record remembers each
class's shortest observed retention; effective expiry is the earlier of the first
stored expiry and occurrence plus that minimum. Lowering policy takes effect on
reads and cleanup; raising configuration or restarting cannot expose expired
history again or silently lengthen newly accepted entries. A longer policy needs
an explicitly designed migration, not a configuration toggle.
The durable policy journal retains at most 16 distinct class names across changes;
additional names fail closed rather than discarding previous minimum policies.

The internal lifecycle worker scans at most 25 entries per pass, once per minute,
with a persisted continuation cursor and repeated full cycles. Expired primary
entries and matching correlation indexes are deleted atomically. There is no
operator update/delete endpoint and no exact physical-erasure SLA. Minimal replay
receipts retain only a source fingerprint and deterministic Audit ID, not actor,
subject or operation payload; they are not expired on an arbitrary TTL that would
permit redelivery to recreate history. Cross-service deletion/backup consistency
and retained-receipt privacy integration remain FIN-286/209.
Storage-deadline cancellation is retried on the next cleanup interval, without
faulting the worker or stopping the host. Actual shutdown cancellation exits
normally; logs contain only the fixed unavailable/retry message.

## Verification Status

Audit Release build passed with zero warnings/errors; all 30 unit/HTTP tests passed,
including new native registration, full-envelope fingerprint, required DTO fields,
safe metadata/legacy compatibility, immutable revision checks and actual native
HTTP safe 503/no-store and authorization-before-storage checks. The isolated
PostgreSQL Audit fixture passed initial and immediate-crash/restart phases;
final review-hardened cleanup verified at `2026-10-29T13:30:10Z`,
installed=false/releaseReady=false.
It exercised separate role/key isolation, concurrent replay and full-envelope
conflicts, callback/missing-append/injected-SQL rollback, first timestamps,
1000-entry correlation capacity and atomic next-append refusal, persisted minimum
retention across lower/raise/restart,31 physical expired-payload/index deletions
and replay that cannot recreate those entries. Revoked-producer and removed-class
historical reads/cleanup passed before and after crash/restart, while new and
identical replay ingestion under revoked admission remained denied. Wrong keys
fail readiness.
Synthetic database and test-host evidence is not installed or live-delivery proof.
Full restore/Release build passed with zero errors/five existing warnings. The
full 1128/1128 baseline across 24 projects passed with no failures/skips, followed
by scoped Audit/fixture builds with zero warnings/errors and 30/30 Audit
regressions after stored-actor and review hardening. New actual-worker tests
exercise storage-deadline/unavailable/conflict retry and shutdown, plus historical
metadata validation independent of current admission. These are distinct runs,
not a second full-suite run inferred from a subset. Staged privacy (1228 files) and
native inventory validation passed. Initial full formatting verified 0 of 1181
changed; final changed-source formatting passed after review fixes.
Markdown (155 files/514 links) passed for implementation delivery. Exact-head
CI/review, actual merge and nine Confluence post-merge full-body readbacks passed.
The guarded closure ledger records one unique local FIN-289 credit before FIN-276
starts; documentation maintenance itself receives no extra completion credit.

FIN-276 owns real delivery/producer wiring, FIN-286 integrated persistence and
privacy recovery, FIN-209 backup consistency, FIN-278/281/206 installed target
qualification and owner testing. No paid functionality, provider, API fallback,
trial, credit/reset redemption, purchase, machine installation or real data.
