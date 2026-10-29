# Native Insight Persistence

FIN-288 is locally Verified Merged through PR291; Jira reconciliation is pending.
Actual merge `c4e6a9e15ab2602aa1d68fc45b48bf4899634e60` at `2026-10-29T10:11:34Z`.
None of this implementation qualifies installed
Windows, live RabbitMQ/Profile ingestion, real notification delivery or first-user
acceptance. FIN-276 owns reliable transport/source integration, FIN-286 integrated
recovery and FIN-278/281/206 installed qualification. No additional spending.

## Owned Boundaries

- Financial Score uses `financial-score`, `fa_financial_score` runtime/schema and
  `fa_financial_score_migration`; its projection/calculation DTOs and keys are its own.
- Recommendations/Notifications uses `recommendations-notifications` and its
  independently provisioned `fa_recommendations_notifications` role/schema/keys
  and migration owner. Its adapter and native composition are implemented in
  the merged implementation, including isolated database recovery verification.
- Income/Expense remain authoritative. Probabilistic wording never supplies score
  factors or changes deterministic financial facts. No cross-service SQL.
- Profile settings are materialized from an authorized API/minimal event, never
  queried from Profile tables. Actual authorized ingestion remains FIN-276.

## Score Transaction Design

The application optionally delegates complete event handling and neutral first-read
creation to an owned transaction. Developer/test adapters retain their behavior.
The native owner lock is cross-process, not an in-process semaphore. It commits
projection/source receipt, affected-currency calculations and immutable outbox
intent together; a throwing or missing publisher cannot commit partial state.
Full minimized envelope fingerprints reject conflicting replay, including changed
correlation, occurrence, category and origin. Separate global owner markers prevent
foreign-owner record/event reuse. Source revisions are independent of SQL CAS.

Version-one, required-field owned DTOs contain a single bounded encrypted owner
aggregate: at most 1000 projections, 1000 source receipts, 1000 calculations and
1000 compact publication intents, additionally bounded to 900000 serialized bytes.
The first limit reached refuses the entire transaction; nothing is silently evicted.
This explicit POC capacity is not an unbounded production history guarantee.
Rows/claims and minimized replay evidence have no arbitrary TTL that could allow
duplicate financial effects. History remains cursor/period compatible, with at
most 101 rows per request, and current follows accepted commit order.

Outbox reads are bounded to 25 intents for an explicitly authorized owner. An
owned-marker scan returns at most 25 owner hashes per page for the internal
dispatcher, without exposing financial payloads or querying other owners' SQL.
An expected-envelope acknowledgement marks the first publication UTC time and cannot
rewrite it; staging is not broker confirmation. Compact acknowledged intents stay
with replay/calculation evidence, so retry after restart cannot recreate a send.
No real provider/broker operation or delivered-notification claim is made.

An authorized Profile snapshot records currency/source version/revision, explicit
budget/onboarding values and UTC observation. Unknown, future or more-than-two-hour
old settings fail closed; only known unconfigured settings supply zero budget.
Committed event replay uses its original calculations, not newer settings or facts.
The deterministic `financial-score-v2` calculator is unchanged. Neutral first reads
persist score 50 without fabricating an event.

## Recommendation And Notification Transactions

Four explicit application boundaries cover Analytics input, Score input,
recommendation-event preparation and deterministic trigger preparation. Each
native transaction commits owned facts, lifecycle/prepared state, minimized source
fingerprints, result identities and immutable publication intent together. Full
source replay returns committed results without regenerating wording, rereading
changed preferences or needing an expired Profile input. Source conflicts and
foreign-owner identifier reuse fail atomically. No shared Score/Analytics SQL.

Required-field version-one DTOs allow at most 32 currency snapshots, 1000 source
receipts, 1000 recommendations, 1000 notifications and 2000 compact publication
intents, additionally bounded to 900000 serialized bytes. The first limit refuses
the whole transaction, never evicts unacknowledged/replay/lifecycle evidence.
Current lifecycle/history queries are bounded by those explicit POC limits;
larger production history and integrated privacy recovery are not implied.

The existing deterministic generator, safe templates and factual explanations
remain authoritative. Native replacement validates generated IDs, codes, severity
and numeric facts. Read/dismiss/expiry and notification read markers cannot be
reset by replay. Current facts preserve existing timestamp/event-ID ordering;
FIN-276 must qualify actual cross-record equal-time/late-source convergence rather
than assuming those hashes provide a causal publication sequence.

Trigger occurrence keys preserve existing daily/currency/code and improved-score
deduplication across different source events. A later source records its own
receipt but neither republishes nor resets an existing matching notification;
recommendation/receipt occurrences remain source-specific. Conflicting replay
still fails atomically.

Notification preferences must have been explicitly stored for that owner. Unknown
preferences fail closed; native code does not invent opt-in from the developer
AllEnabled default. Push/web flags, enabled types and quiet-hours/time-zone metadata
persist atomically with concurrent lifecycle updates. Preparation is not a send:
quiet-hours/provider delivery enforcement remains an integration acceptance gate.
No paid provider is called or activated. In-app state can be developed and tested
without an external push provider.

`ConfirmsDelivery=false` means durable staging, so application preparation does
not prematurely mark publication complete. Exact-envelope acknowledgement only
marks the first outbox publication time; it never claims end-user delivery.
Pending reads return at most25 per owner/kind, owner discovery at most25 hashes.
Gateway-supplied native Delivered transitions and direct publication completion
are rejected. Explicit undelivered attempt/retry metadata is stored monotonically
without sending; synthetic attempt/ack hooks are not real-provider evidence.

Authorized Profile input is independently materialized by owner/currency/source
version/revision and observation time. Unknown/future/older-two-hour input fails
closed; known unconfigured budget is null, not invented state. No Profile table
access or external API call occurs under the owned SQL transaction. FIN-276 owns
actual authorized ingestion. Native hosts require their own Postgres settings and
keys, expose safe503/no-store on unavailable state, and retain unqualified delivery
readiness rather than selecting memory or legacy consumers.

## Verification So Far

On 2026-10-29 Score 29/29 and Recommendations/Notifications 41/41 unit tests passed.
The expanded combined PostgreSQL fixture passed initial and immediate-crash/restart
phases, with cleanup verified at `2026-10-29T09:17:06Z`. It exercises separate
Score/Recommendation roles, SQL isolation, encrypted state, concurrent replay,
neutral first reads, exact decimal/factor recovery, stale/conflicting sources,
missing-publication and injected-SQL rollback, lifecycle/preferences/read markers,
Score-input recommendations, deterministic trigger deduplication, bounded outboxes,
first acknowledgement and undelivered retry metadata. No services were installed
and `releaseReady=false`. Full restore/Release build passed with zero errors and
five existing warnings; all 1119 tests across 24 projects passed, none skipped.
Markdown 154 files/500 links and the unqualified native inventory passed.
Full formatting verified zero changes across 1170 files after mechanical fixes.
The staged 1216-file privacy baseline passed. Confluence's eight affected
current-status pages were updated and their complete bodies verified.
Final head `5b9a5c04571011cbb86b2fc9b4145d7e9a89d09a` passed all five CI checks
(Backend40853974694, Mobile40853974669, Admin40853974692). Configured review
completed at10:01:35UTC with no findings; final channels clear. Protected
expected-head squash and independent merged=true read verified the actual merge.
All eight affected Confluence post-merge full-body readbacks passed.
Temporary local tracking is owner-approved in
[the POC backlog](../agent/LOCAL_BACKLOG.md); no unread Jira status is fabricated.

## Outstanding Work

FIN-288 implementation delivery is verified; its closure ledger is published
separately, with Jira reconciliation pending during the outage.
Actual authorized source ingestion, reliable delivery and integrated installed
recovery remain separate dependent work. Local completion is backed by the actual
merge and all delivery evidence, not a partial local build. Reconcile
actual Jira evidence/transitions only when Jira is restored.
