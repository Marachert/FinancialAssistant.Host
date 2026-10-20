# Native Projection Persistence

FIN-287 implementation work for the separate Financial Summary module and
Analytics owner. Income and Expense remain financial sources of truth. This
document distinguishes implemented source from unfinished integration; it is not
an installed-native or first-user acceptance claim. Actual ticket closure is recorded
in the [delivery ledger](../agent/POC_PROGRESS.md), not inferred from source alone.

## Summary Ownership

`PostgresFinancialSummaryReadModelStore` implements the existing Summary store
interface using `financial-summary`, `fa_financial_summary` and its independent
runtime/migration roles. The module does not add an SCM service, API or port and
cannot borrow Analytics' role or query financial-owner SQL. Use separately
protected credentials/index/encryption keys and the
[foundation migration/grant contract](../../backend/shared/persistence/README.md).
No automatic migration, credential generation or machine provisioning occurs.

Strict versioned snapshots require every field, reject unknown members and retain
exact decimals, currency, owner hash, type/record/category, origin, source revision,
event identity and UTC time. Source revisions are independent of storage CAS
revisions. Opaque indexes and authenticated encrypted payloads remain service and
owner bound. Record/event ownership markers prohibit reuse by another owner.

Each owner mutation atomically commits its projection, durable latest-revision
head, immutable event fingerprint, ownership markers and counters under bounded
owned PostgreSQL locks. Duplicate events are no-ops; changed event content or
equal-revision disagreement conflicts. Stale revisions do not roll back a newer
currency move, correction or archive. The unchanged application calculator excludes
archived rows and derives exact daily, Monday-based weekly, monthly and category
totals; freshness includes the latest archived event. No currency mixing or hidden
financial-service fallback is introduced.

Reads paginate in batches of100 and fail on count/identity/schema inconsistencies.
The explicit POC bound is1000 active projection records and10000 immutable source
receipts per owner. Reaching either bound fails closed; it never evicts financial
projections or silently forgets replay protection. Latest-revision heads and
receipts survive generation cleanup. These limits require an explicit, verified
capacity/replay/privacy lifecycle before expansion, not an arbitrary TTL.

## Scoped Summary Rebuild

The native adapter rejects `ResetAsync`; the developer projector's global reset
is not a native rebuild API. Internal rebuild methods use an authorized source
snapshot, owner hash, inclusive period, opaque source version, initialized UTC
request time, expected count and deterministic chained source digest. Bounds
match the existing Analytics plan: at most3650 days and128 source-version
characters. The helper computes the digest from canonical record-key order and
fixed100-record pages. A matching digest verifies supplied content, not source
authorization; the caller must obtain an authorized financial-owner export/event
snapshot. No untrusted public execution endpoint or cross-owner SQL export exists.

One persisted rebuild slot per owner records source identity, captured live CAS
watermark, generation, page fingerprints/counts and status. Exact begin/page
retries are idempotent; changed manifests/pages, duplicate/out-of-order keys,
foreign-owner/out-of-period records and incomplete source data are rejected.
Staged rows remain invisible across a crash. Commit verifies the complete source
digest/count, no intervening live mutation, compatible current/latest revisions,
owned identities, capacity and exact-decimal overflow safety. It preserves records
outside the requested period and atomically switches the active generation with
its counters, replay receipts, heads and completed job state. A stale watermark
requires cancellation/cleanup and a new source attempt, never overwrite of live
work. Empty verified snapshots remove only the targeted period.

Immutable source receipts and latest-revision heads are independent of active
generations: omitted rows cannot be recreated by delayed/stale events. A genuinely
newer event can project again. Cancellation cleans staging; success cleans only
the retired generation. Each cleanup transaction removes at most25 rows, refuses
the active generation and survives retries/restarts. Another job cannot accumulate
generations while prior cleanup remains pending. No physical financial record is
deleted by projection cleanup.

## Analytics Ownership And Publication

`WindowsNative` binds `Analytics:Storage:Postgres`, validates all required options
and protected keys and registers only the Analytics-owned context, projection store,
outbox and settings snapshot provider. The native catalog requires its storage
Password and IndexKey. Summary uses its typed `NativeFinancialSummaryStorage`
factory with separately provisioned settings; its explicit migration helper uses
the module migration role, never another host's singleton/credentials. Neither
factory silently migrates or repairs machine state at runtime.

Analytics owns separate DTOs and `fa_analytics`; it does not depend on Summary's
business model. Both native and developer adapters use `AnalyticsSnapshotBuilder`
for exact daily/weekly/monthly/category calculations. Native totals come from the
bounded durable projections rather than a volatile cache. The same explicit1000
record/10000 source-receipt bounds and independent revision heads apply. Canonical
decimal fingerprints remove representation-only trailing zeros without floating
point or rounding; ordinary stored snapshots retain exact decimal values.

`IAnalyticsProjectionTransaction` encloses the minimized source fingerprint,
projection/revision/owner state, affected old/new currency set and immutable
`analytics.updated.v1` intents in one owned transaction. Publisher/unknown settings
failure rolls everything back. No HTTP, broker, provider or cross-owner SQL runs
under the owner lock. A fully committed source replay verifies its original hash
and stored pending intents without recalculating totals or refreshing settings.
Changing financial content or source correlation metadata conflicts. This is an
owned commit receipt, not completed FIN-276 broker inbox qualification.

The native publisher has `ConfirmsDelivery=false`: staging is not delivery.
`MarkPublicationCompletedAsync` is deliberately unsupported in the native store.
Only an acknowledgement matching the complete immutable outbox envelope can set
its first UTC published time and complete that source/currency in the same
transaction. A currency move has two independently durable completion entries.
Later projections cannot rewrite an older event's first facts. Pending scanning
returns at most25 entries with a persistent rotating cursor over at most ten
100-row pages. Cleanup scans at most25 rows and deletes only acknowledged intents
older than30 days; pending messages and minimized replay heads/receipts remain.
The exposed acknowledgement/cleanup hooks do not contact a broker or claim one did.

## Settings And Readiness

`PostgresAnalyticsLimitProvider` stores an authorized materialized settings snapshot
per owner/currency with source version/revision, three independently nullable limits
and initialized UTC observation time. Revisions cannot roll back or conflict;
matching newer observations can refresh the same known source. Reads require an
observation no older than two hours and never in the future. Unknown, expired or
invalid state fails closed; an explicitly known all-null snapshot means genuinely
unconfigured, never a fabricated zero or a volatile fallback. Snapshot ingestion is
internal and has no public setter. This does not create Profile-owned settings in
Analytics or authorize an untrusted caller to declare a source authoritative.

The current Profile/API event inventory does not yet supply this existing limits
boundary. Authorized settings/source ingestion, reliable consumers and broker
confirmation remain FIN-276. Native mode does not start the unqualified legacy
consumer or register a memory/RabbitMQ publisher fallback. Storage health remains
unhealthy for pending lifecycle delivery even when PostgreSQL is available. Native
HTTP responses use `no-store`; storage/unknown-settings failure produces a safe503
without internal identifiers or exception details. Developer/Testing contracts and
the plan-only public rebuild surface remain compatible.

## Analytics Rebuild Stage

The owned executor accepts the existing `AnalyticsRebuildRequest` and preserves
the canonical planner job key. A separate deterministic generation identity is
internal storage metadata. It uses bounded immutable pages, count/digest checks,
captured live CAS watermark, independent latest-revision heads, atomic scoped swap
and bounded retired/cancelled generation cleanup, as for Summary. Its progress is
explicitly the `rebuild-analytics` stage, not success for the full ordered plan.

Swap atomically records a durable refresh handoff for all affected old/new
currencies. It never fabricates a financial-source event or writes Score or
Recommendation tables. The coordinator reads the pending source/generation/scope
and current owned revision; a completion hook requires that exact current handoff.
An intervening live write invalidates an older acknowledgement. Another rebuild
cannot accumulate generations while cleanup or refresh is pending. The hook is a
testable integration boundary, not evidence that downstream execution occurred;
FIN-276/288 retain the remaining independent owner stages and convergence.

## Free Verification And Remaining Work

After building the backend solution in Release, on Windows run:

```powershell
pwsh -NoProfile -NonInteractive -File tools/scripts/test-owned-postgres.ps1 -Scenario Projections -ArchivePath <verified-vendor-zip>
```

The fixture uses the existing exact-manifest-hash PostgreSQL archive in a fresh
ignored loopback cluster, independently provisions Summary and Analytics roles,
verifies SQL isolation, crash-stops/restarts only its owned process and cleans up.
Synthetic Summary checks cover exact totals, currency/archive behavior,
concurrent replay/conflicts, owner isolation,101-record pagination, injected
mid-write rollback, incomplete/stale rebuild rejection, idempotent pages,
interrupted staging recovery, atomic scoped/empty replacement, outside-period and
other-owner preservation, bounded cleanup and stale non-resurrection.

Analytics checks cover exact durable totals, atomic source/projection/outbox state,
immutable first publication after later state, independent currency acknowledgement,
forged-ack denial, pending-safe retention and publisher/null-publisher rollback.
Durable known/unconfigured settings survive restart; unknown/expired sources fail,
and committed replay remains possible while settings are expired. Both owners are
also exercised at the1000-record cap with ten staging pages, atomic swap/read,
capacity refusal and existing-record correction after actual restart. Analytics
interrupted scoped staging, canonical planner identity, live-watermark rejection,
exact totals, omitted-row replay guards and stale refresh acknowledgements are
checked independently. Synthetic refresh acknowledgements are not cross-owner
execution or live delivery.

The existing rebuild plan does not execute Score or Recommendation stages;
those owners retain their own state under FIN-288. Reliable source/transport/inbox
qualification remains FIN-276, integrated retention/recovery FIN-286 and backup
acceptance FIN-209. Ticket closure requires its full scoped evidence, reviews,
exact-head CI, actual merge and documentation gates; consult the live ledger.

No real identities/financial data, paid provider/API/runner/credit/reset/fallback,
SCM registration, production migration or extra spending is authorized. WPF,
installed Windows11/Server2022/2025 and first-user acceptance remain pending.
