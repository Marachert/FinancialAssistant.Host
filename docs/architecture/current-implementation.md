# Current Implementation And Product Target

Reviewed for FIN-269 against merged FIN-194, PR #247, commit
`783f22746d3902181153076b0871121311e8f4f7` (2026-09-10).
Use the [live closure ledger](../agent/POC_PROGRESS.md) for subsequent progress;
this page describes capability boundaries, not deployed-environment acceptance.

## Capability Map

The 2026-09-15 [Windows-native deployment decision](windows-native-poc.md)
supersedes the Compose POC target. FIN-270 is implementation work: no WPF
installer or accepted native Windows runtime is claimed by this document.

| Area | Implemented repository baseline | Separate target or runtime gate |
| --- | --- | --- |
| Gateway | REST routing, JWT/role checks, exact public allowlist, trusted context, rate limits and safe errors | Route activation and destinations are environment-controlled; not every handler is enabled by repository defaults |
| Identity/Profile/Category | Deterministic authentication/session/provider-validation handlers, profile preferences and taxonomy | Stable secrets, durable stores and real provider configuration; phone delivery adapter is disabled by default |
| Intake/Receipt/AI | Editable drafts, review/reject/confirm, receipt/OCR processing and controlled provider abstractions | Approved provider budgets, privacy configuration and end-to-end hosted operation |
| Income/Expense | Separate native PostgreSQL authoritative records, atomic minimized outbox, immutable confirmation replay and lifecycle contracts | Live delivery/inbox FIN-276, integrated recovery FIN-286 and installed acceptance |
| Summary/Analytics | Separate owned PostgreSQL projections, strict replay and scoped generation recovery; deterministic dashboard composition | Authorized source ingestion, reliable transport and cross-owner refresh FIN-276; integrated/installed acceptance |
| Financial Score | Owned PostgreSQL projections, deterministic score factors/history, neutral first reads and atomic immutable outbox | Authorized settings/source ingestion and reliable delivery FIN-276; integrated/installed acceptance |
| Recommendations/Notifications | Owned PostgreSQL facts/lifecycle, explicit preferences, trigger deduplication, prepared/read state and atomic immutable outbox | Push/web adapters are non-sending placeholders; no real delivery or deferred quiet-hours scheduler claimed |
| Mobile | Home/Add/Insights/Settings, authentication/onboarding, text/receipt draft review, dashboard, inbox and resilience states | Native audio, complete wallet/debt/reserve flows, signed stores and tester acceptance are not all delivered |
| Monitoring/Audit/MCP | Safe operational aggregates, append-only audit contracts and six allowlisted read-only MCP tools | Durable history, complete producer wiring and approved operational acceptance |
| Admin web | Protected React dashboard, memory-only sessions, service status, bounded recent/failed jobs and AI/OCR summaries | Support lookup is disabled; job history is process-local (200 observations, 24 hours), not a durable job scheduler |
| Infrastructure | Retained legacy Compose topology/runbooks; versioned native inventory and [Windows Service process hosting](../../backend/shared/hosting/README.md) | WPF installer, actual registered Windows Service acceptance, durable adapters, approved host/TLS/secrets/recovery and real client installation (FIN-270) |

The internal admin app is not a consumer web application. The product still targets
Android, iOS and Web, but repository directories alone do not establish delivery.

FIN-282 adds [native Identity PostgreSQL adapters](../engineering/identity-native-persistence.md)
for accounts, sessions, challenges/rate state and transactional outbox intents.
Development adapters remain process-local. Native broker delivery/readiness is
explicitly pending FIN-276. FIN-283 adds [separate Profile/Category PostgreSQL
adapters](../engineering/profile-category-native-persistence.md), exact preferences/
catalog rehydration, owner-isolated updates and atomic Category/outbox intents.
Other domain adapters remain their own follow-on tickets. This is not installed-host
or first-user acceptance; developer/test adapters remain process-local.

FIN-275 adds [native Income/Expense persistence](../engineering/financial-records-native-persistence.md)
with atomic record/outbox commits, immutable original replay checks and exact
financial rehydration. Confirmation remains event-driven; transport is not
implemented by the storage adapters. Developer/test stores stay process-local.

FIN-284 adds [native Intake drafts and confirmation intent](../engineering/intake-native-persistence.md):
atomic owned indexes/state/outboxes, explicit bounded encrypted source retention,
replay/rollback/restart safety and parsing outside SQL. Native Intake registers no
financial-owner stores/consumers and does not promise synchronous owner visibility.
Live transport/inbox, orchestration and installed acceptance remain separate.
Implementation PR283 was independently verified merged as
`48c7c0941b4b58cf3f7884c5c16182a9803f6594` on 2026-10-15; FIN-284 is Done.
FIN-277 is Done after actual PR285 merge `dfe9692f1da22aa22e864d4eb9932d5e67316af0`
at `2026-10-15T18:24:45Z`. FIN-285 adds
[Receipt/OCR and AI workflow persistence](../engineering/receipt-ai-native-persistence.md)
on those protected native objects: atomic metadata/index/outbox state, exact candidate
decimals and separate AI metadata/usage/request reservations. No file-only success,
volatile native workflow fallback or paid provider is permitted.
FIN-285 is Done after actual PR287 merge `8a3d53f9508de9eb1577ff377dbe49c73403e4a5`
at `2026-10-19T20:26:56Z`, final297354d/all5CIgreen and zero-total review fixed.
Its closure census is192/215(89.3%), FIN-27010/19 and P9 16/39(41.0%);
native delivery, remaining owners, installer and first-user gates remain required.

FIN-287 adds [separate native Summary/Analytics projections](../engineering/native-projection-persistence.md):
module-owned Summary credentials without another host/port, exact durable read models,
immutable replay/latest-revision guards, atomic Analytics publication intents and
scoped staged rebuilds. The native settings provider distinguishes explicitly known
unconfigured values from unavailable/expired input; native delivery readiness stays
unhealthy until FIN-276. An Analytics-stage refresh handoff is not completed Score
or Recommendation execution. Synthetic database evidence does not certify installed
Windows, live broker/settings ingestion or first-user readiness.
FIN-287 is Done after independently verified actual PR289 merge
`e6b8f6abc8dac24911eafda65f6515a7feed384a` at `2026-10-20T21:12:28Z`,
finalf697631/all5CIgreen/completedreview no findings. Its fresh closure census is
193/215(89.8%), FIN-27011/19 and P9 17/39(43.6%); first-user Not Ready.

FIN-288 adds [native insight persistence](../engineering/native-insights-persistence.md):
separate Score and Recommendations/Notifications roles, deterministic factors and
history, atomic receipts/outboxes, lifecycle/read state and explicit settings/
preferences. The combined isolated PostgreSQL initial/crash/restart fixture passed
on 2026-10-29. PR291 is independently verified merged as
`c4e6a9e15ab2602aa1d68fc45b48bf4899634e60` at `2026-10-29T10:11:34Z`,
final5b9a5c0/all5CIgreen/completedreview no findings. At FIN-288 closure, local fixed-scope progress was
194/215(90.2%),P918/39(46.2%),FIN27012/19; last live Jira baseline193/215 remains
separate with reconciliation pending. First-user Not Ready. No live broker/Profile
ingestion, actual notification send or installed acceptance is claimed. Native
hosts fail closed rather than selecting memory/legacy consumers. While Jira is
suspended, [owner-approved local tracking](../agent/LOCAL_BACKLOG.md) records scope
without fabricating Jira transitions or awarding unfinished-work progress.

FIN-289 adds merged [native append-only Audit persistence](../engineering/native-audit-persistence.md)
with independent PostgreSQL roles/keys, immutable replay receipts, bounded
correlation lookup and durable minimum-retention/cleanup state. Isolated database
initial/crash/restart and native HTTP failure/access tests passed. PR293 is
independently verified merged as `f870733b35bc1d74a4248f882d4426faeef1e92e` at
`2026-10-29T13:53:43Z`, final77adda4/all5CIgreen, both valid findings fixed/tested/
evidenced/resolved after green and completed re-review no findings. Nine post-merge
Confluence full bodies were verified. Local progress is195/215(90.7%),P919/39(48.7%),
FIN27013/19open; last live Jira baseline remains193/215, pending reconciliation.
First-user Not Ready. Audit remains minimized technical metadata, never
financial authority or raw financial/provider content. Live producer/transport
wiring, integrated privacy/backup recovery and installed acceptance remain separate.

The [FIN-275 ownership inventory](../engineering/native-financial-persistence-scope.md)
separates its original seven-owner persistence scope into FIN-275 financial records/
event confirmation, FIN-287 Summary/Analytics, FIN-288 Score/Recommendations/
Notifications and FIN-289 append-only Audit. Planning does not implement those siblings;
FIN-284 follows its verified financial-owner prerequisite rather than writing those
tables or using native in-process financial repositories. Confirmation remains
Intake outbox -> FIN-276 -> matching owner inbox/store/outbox; owner APIs only
reconcile status for Intake and never introduce another write path. All siblings remain
required for FIN-270 and integrated FIN-286 acceptance.

## End-To-End User Scenario

FIN-277 adds [protected native adapters](../engineering/windows-native-adapters.md):
restart-safe encrypted receipt objects with ACL/path/key/retention controls,
disposable bounded service-health observations and a private technical diagnostic
sink. FIN-285 native Receipt metadata fails closed when owned storage is invalid; native reliable
delivery remains FIN-276. The local sink does not qualify retained native collectors,
Elasticsearch, Audit authority or installed-host acceptance. No live provider is enabled.

The implemented contracts support this flow when the required routes, adapters
and environment are enabled and verified:

1. Authenticate through the public gateway and complete profile/onboarding.
2. Submit synthetic text or a receipt; Intake/Receipt/AI produce a draft with
   candidate fields, confidence and explicit ambiguities.
3. Review, correct, reject or confirm. Confirmation validates deterministically
   and records Intake outbox intent; the matching Income/Expense commit follows
   asynchronously, never a synchronous write RPC. AI never writes the ledger.
4. Owned events feed projections, dashboard/score and recommendation preparation.
5. Read the resulting financial overview and inbox through protected APIs.
6. Operators inspect safe monitoring/audit signals, not raw user input or receipts.

Failures retain explicit loading/error/retry/review states. A failed provider,
unconfirmed draft, disabled route or non-sending notification adapter must not be
reported as a successful financial write or notification delivery.

See [financial core flow](financial-core-e2e-flow.md),
[mobile UX](../product/mobile-poc-ux.md), and
[Confluence diagrams](https://marachert.atlassian.net/wiki/spaces/FA/pages/557178).

## Known Concept Differences

- Current mobile navigation uses four tabs. Earlier no-bottom-navigation and
  all-circular-metric concepts are targets, not descriptions of every screen.
- Text and receipt capture are implemented. A voice-transcript source enum does
  not prove native audio recording or transcription exists.
- Current profile bootstrap defaults are en-US/UTC/USD. Ukrainian/UAH is product
  intent, not an already-applied default change.
- A period's income-minus-expense flow is not a bank/cash/available balance.
  Full wallet, transfer, debt and reserve APIs in earlier concepts are not all
  implemented by Summary or Analytics.
- Current amount contracts use decimal amounts. Older `amountMinor` design
  examples must not cause silent scaling by 100 or incompatible renaming.
- [Storage policy](storage-policy.md) distinguishes preferred PostgreSQL
  durability from existing in-memory and Elasticsearch contracts.
- Identity still emits legacy `user.signed_in.v1`; the newer shared naming rule
  rejects underscores. Preserve compatibility until a versioned migration is
  explicitly implemented, tested and documented.

## Acceptance Boundary

POC percentage counts canonical Jira leaves, not elapsed time or production
readiness. First-user testing remains Not Ready until the ledger's runtime,
privacy, provider-cost, integration, deployment and client-installation gates have
evidence. Do not invent a calendar ETA, completed restore drill or live delivery.

Implementation references: [Identity sessions](../engineering/identity-session-lifecycle.md),
[gateway groups](../engineering/gateway-public-api-groups.md),
[notification adapters](../engineering/notification-delivery-adapters.md),
[admin API](../api/monitoring-admin-v1.md), [MCP contract](../api/mcp-server-v1.md),
[release tests](../engineering/backend-release-test-suite.md),
[Windows POC](../../infra/windows-poc/README.md).
