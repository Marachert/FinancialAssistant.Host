# Native Financial Persistence Ownership

FIN-275 ownership inventory and decomposition, 2026-10-11. This is scope planning,
not implemented persistence or installed acceptance. No original requirement is
removed, and no extra product features or completed-ticket credit are introduced.
Follow the [storage policy](../architecture/storage-policy.md),
[financial authority](../architecture/financial-source-of-truth.md) and
[PostgreSQL foundation](../../backend/shared/persistence/README.md).

## Inventory And Delivery Leaves

| Owner | Existing boundary and state | Native delivery leaf |
| --- | --- | --- |
| Income | `IIncomeRecordStore`: confirmed/manual records, owner/transaction indexes, decimal/currency/origin/status/revision/time; `IIncomeRecordEventPublisher` | [FIN-275](https://marachert.atlassian.net/browse/FIN-275) |
| Expense | `IExpenseRecordStore` and `IExpenseRecordEventPublisher`, with the same independent financial-owner lifecycle | FIN-275 |
| Financial Summary | `IFinancialSummaryReadModelStore`: owner-hash/currency/record/revision projections and disposable rebuild state | [FIN-287](https://marachert.atlassian.net/browse/FIN-287) |
| Analytics | `IAnalyticsReadModelStore`: materialized totals, source-event/revision state and publication completion; server-owned limit-provider boundaries | FIN-287 |
| Financial Score | `IFinancialScoreStore`: projections/calculations/current/history/source-event lookup; authorized Profile-settings snapshots and publisher | [FIN-288](https://marachert.atlassian.net/browse/FIN-288) |
| Recommendations/Notifications | `IRecommendationNotificationStore`: facts, lifecycle/status, source completion, prepared/read/delivery markers; Profile snapshots, notification preferences/quiet-hours metadata and separate publisher boundaries | FIN-288 |
| Audit | `IAuditRecordStore`: immutable minimized metadata, matching-event append replay, correlation lookup, occurrence/record/expiry times and actor/subject hashes | [FIN-289](https://marachert.atlassian.net/browse/FIN-289) |

These are seven existing storage owners in four sibling leaves under FIN-270,
not a new shared business repository. Their present adapters are process-local;
the planning change does not replace them. Keep separate schemas/roles/keys and
owned DTOs. Shared persistence is technical infrastructure only.

Financial Summary is presently a module without a standalone API/native host.
The current `StorageIdentity` allowlist follows host identities, so FIN-287 must
explicitly declare and test the module's owned storage identity/provisioning.
Do not reuse another owner's role or create a new deployable/port by implication.
Audit metadata belongs to FIN-289, while FIN-277 still owns diagnostic collectors/
sinks and manifest qualification; no monitoring/audit capability is removed.

## Prerequisite Order

The next ranked leaf FIN-284 is blocked by FIN-275. Follow this recorded prerequisite
before native Intake confirmation work; do not mark a blocked ticket Done or use
in-process financial repositories to avoid the owner boundary.

- FIN-275 depends on completed FIN-274 and delivers authoritative Income/Expense
  storage plus authenticated confirmation/reconciliation APIs for FIN-284.
- FIN-287 depends on FIN-274/275 for separate Summary/Analytics projections.
- FIN-288 depends on FIN-275/287 and the completed FIN-283 Profile contract.
- FIN-289 depends on FIN-274 and preserves the existing FIN-193 audit policy.
- FIN-286 explicitly depends on FIN-275/287/288/289 in addition to the earlier
  domain leaves. FIN-276 still owns reliable transport/inbox qualification.

Jira rank is local to the parent. Prerequisites override selection order; all
siblings remain required for FIN-270. A scope-planning PR does not complete FIN-275.

## Acceptance Boundaries

FIN-275 preserves exact deterministic financial values, per-currency totals,
owner isolation, manual/confirmed origins, immutable matching confirmation replay,
revision ordering, corrections and archive/restore. A financial commit and its
minimized outbox must be atomic. Intake uses authenticated owner APIs, never
financial tables or native in-process repositories. Do not hold SQL locks over
external HTTP/broker calls. No new physical financial-delete endpoint is authorized.

FIN-287 preserves rebuildable owner/currency projections, currency moves,
archived-record exclusion, freshness and source/publication state. Rebuild stages
are bounded and use verified scoped shadow generations with atomic replacement;
an indiscriminate native global reset or silent owner-SQL fallback is not acceptance.

FIN-288 preserves deterministic score factors/history, neutral first read,
recommendation lifecycle, notification preferences/triggers/quiet-hours/read and
delivery metadata. Profile input comes only from authorized APIs/minimal events.
No wording provider can alter financial facts or score contributions. Provider
placeholders cannot claim sending or successful notification delivery.

FIN-289 preserves append-only matching replay and conflict rejection, allowlisted
privacy-safe metadata, correlation lookup and policy-derived expiry. The native
target follows PostgreSQL storage policy; older Elasticsearch alias contracts are
retained as historical/compatibility references, not financial authority. Keep
retention-class bounds and bounded physical lifecycle cleanup without an operator
update/delete endpoint. Never include raw identities, amounts, notes, receipts,
OCR, prompts, credentials or other financial payloads in Audit.

Each implementation requires free synthetic deterministic/concurrency/replay/
owner-role-isolation/retention/migration/rollback/crash-restart evidence. Native
configuration/state/readiness fail closed, without in-memory fallback or fake
publication. Preserve developer/test adapters. Installed Windows 11/Server
2022/2025, SCM, WPF/package/recovery and first-user acceptance remain separate
FIN-278/280/281/206 and owner-run gates. No additional spending or paid provider,
runner, credit redemption, trial, certificate, API fallback or production mutation.

## Scope Statistics

The fresh 2026-10-11 18:42:32 UTC Jira census contains 289 unique issues. Three
new unfinished sibling units change 188/212 (88.7%) to 188/215 (87.4%). The -1.2
percentage-point adjustment is calculated before rounding (-1.2374 pp), reflects
granularity only, and loses no completed work. P9 becomes 12/39 (30.8%); FIN-270
has 6/19 children Done and remains In Progress. First-user testing is Not Ready.
Use the [current progress ledger](../agent/POC_PROGRESS.md); historical closure
percentages remain valid for their original scope and are not rewritten.
