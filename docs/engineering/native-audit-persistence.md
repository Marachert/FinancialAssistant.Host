# Native Audit Persistence

FIN-289 is locally In Progress; implementation is unmerged. Isolated PostgreSQL
verification passed; no installed Windows, live transport, full privacy
recovery or first-user acceptance is claimed. Jira is suspended; use the
[owner-approved local backlog](../agent/LOCAL_BACKLOG.md), without paid access or
fabricated transitions. Last published local completion remains 194/215 (90.2%).

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

## Verification Status

Audit Release build passed with zero warnings/errors; all 25 unit/HTTP tests passed,
including new native registration, full-envelope fingerprint, required DTO fields,
safe metadata/legacy compatibility, immutable revision checks and actual native
HTTP safe 503/no-store and authorization-before-storage checks. The isolated
PostgreSQL Audit fixture passed initial and immediate-crash/restart phases;
final hardened-adapter cleanup verified at `2026-10-29T12:07:33Z`,
installed=false/releaseReady=false.
It exercised separate role/key isolation, concurrent replay and full-envelope
conflicts, callback/missing-append/injected-SQL rollback, first timestamps,
1000-entry correlation capacity and atomic next-append refusal, persisted minimum
retention across lower/raise/restart,31 physical expired-payload/index deletions
and replay that cannot recreate those entries. Wrong keys fail readiness.
Synthetic database and test-host evidence is not installed or live-delivery proof.
Full restore/Release build passed with zero errors/five existing warnings. The
full 1128/1128 baseline across 24 projects passed with no failures/skips, followed
by final scoped Audit/fixture builds with zero warnings/errors and 25/25 Audit
regressions after stored-actor hardening. These are distinct verification runs,
not a second full-suite run inferred from a subset. Staged privacy (1227 files) and
native inventory validation passed. Full formatting verified 0 of 1181 changed;
Markdown (155 files/513 links) passed. Nine affected Confluence working-state full-body
readbacks were verified. Exact-head CI/review, actual
merge and Confluence post-merge publication remain required. No new progress credit.

FIN-276 owns real delivery/producer wiring, FIN-286 integrated persistence and
privacy recovery, FIN-209 backup consistency, FIN-278/281/206 installed target
qualification and owner testing. No paid functionality, provider, API fallback,
trial, credit/reset redemption, purchase, machine installation or real data.
