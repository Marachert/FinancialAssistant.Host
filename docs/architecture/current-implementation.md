# Current Implementation And Product Target

Reviewed for FIN-269 against merged FIN-194, PR #247, commit
`783f22746d3902181153076b0871121311e8f4f7` (2026-09-10).
Use the [live closure ledger](../agent/POC_PROGRESS.md) for subsequent progress;
this page describes capability boundaries, not deployed-environment acceptance.

## Capability Map

FIN-276 merged implementation adds opt-in native RabbitMQ producer/outbox and
consumer/inbox workers for confirmed financial changes through Analytics, Score,
Recommendation preparation and minimized Audit, plus scoped Profile HTTP input.
Actual synthetic financial convergence/crash/restart/replay passed locally,
including the repository-owned runner, without manual projector or provider calls.
PR295 is actually merged as `d142b075dced5847da8f12f9d237763e50d06e2c`; local verified
completion after FIN-286 is 197/215 (91.6%), P9 21/39 (53.8%), FIN-270 15/19 open.
Installed and first-user gates below remain distinct. See the
[native lifecycle evidence and limitations](../engineering/native-lifecycle-delivery.md).

FIN-286's [full native persistence qualification
matrix](../engineering/native-persistence-qualification.md) is locally Verified Merged
through PR297, actual `d1704ab108892d8ee7007633066d5d9a21d751d5`, with Jira
reconciliation pending. All nine actual database scenarios and separate native-file
recovery passed, including live uncommitted domain/outbox interruption. This does
not grant installed or first-user acceptance.

FIN-207's [native HTTPS increment](../engineering/windows-native-https.md) is
actually merged through [PR299](https://github.com/Marachert/FinancialAssistant.Host/pull/299),
protected squash `f3e117ce127d4f204bb2a88d568badaf6ad3e358` at
`2026-11-02T14:30:58Z`. Final reviewed head
`d057950852f78b163bef5ca913384c38fa1431d0` passed all five checks, configured
re-review and full1344/1344 local tests. The valid proxied-body413 finding was
fixed and regressed through real dispatcher/exception components before resolution
after green. FIN-207 remains open for FIN-278/approved-host qualification; this
implementation merge adds no POC completion credit or first-user readiness.

FIN-208's [protected configuration runtime component](../engineering/windows-native-protected-configuration.md)
is actually merged through [PR301](https://github.com/Marachert/FinancialAssistant.Host/pull/301),
protected squash `63fe6b12e8ba2e9cd6f7e24a16b52c91cf74cfb6` at
`2026-11-02T20:45:37Z`. Final reviewed head
`b2072ab474616f6eb854619555d66da28814e7ea` passed all five checks, configured
re-review and final full1451/1451 local tests. Six new regressions reproduced and
fixed the valid missing-packaged-baseline startup finding before evidence-based
resolution after green. Actual privileged provisioning, admin bootstrap,
coordinated rotation, WPF/recovery integration and approved-host qualification
remain pending. FIN-208 stays open and adds no completion credit or readiness.

FIN-209's [portable authenticated recovery primitives and owned
Income/Expense recovery drill](../engineering/windows-native-recovery.md) are
actually merged through [PR303](https://github.com/Marachert/FinancialAssistant.Host/pull/303),
protected squash `379e6fc630b62aa0f1f269188db89c3e45ac66bd` at `2026-11-03T08:21:44Z`,
final `4515a8229b89a43a0e94866d2972369b84df8237`. All five exact-head checks and
configured review passed with no findings; local full1484/1484 passed.
The drill restores separately encrypted schemas and key bundles into a new empty
target, proves wrong-key denial, exact financial reconciliation and immutable
replay, then repeats after an actual database crash/restart. This is a bounded
two-owner component, not complete POC backup, installed acceptance or completion
credit. All-owner consistency, objects, protected configuration, scheduling and
FIN-278/280 integration remain open.

The follow-on FIN-209 manifest validator is actually merged through
[PR305](https://github.com/Marachert/FinancialAssistant.Host/pull/305), actual
`edc4a76155d68ebed032bbca06845de7b810c000` at `2026-11-03T08:57:26Z`, final
`5dc07721b8dcef2bccd8a7228ed0810e414481d7`, all five checks/review clear and
full1528/1528 local tests. It binds a complete 30-resource inventory to
authenticated artifact identities, lengths, digests, schema/migration metadata
and conservative broker/search policies. It deliberately rejects the four-
artifact drill as complete POC backup. Metadata validation does not perform
quiescence, capture, bundle-content reconciliation or target activation; see
the [recovery boundaries](../engineering/windows-native-recovery.md).

The portable package layer is actually merged through
[PR306](https://github.com/Marachert/FinancialAssistant.Host/pull/306), actual
`ce26f8344982b884e2f5ba0cb4c01fc1b6fd5ce6` at `2026-11-03T09:35:20Z`, final
`ab4297866f02158bd4ee8d1573e68bdab7e74723`, all five checks/review clear and
full1551/1551 local tests. It constructs and writes the complete encrypted inventory
to caller-owned empty streams, and returns an imported package only after its
manifest and every artifact authenticate. This is not actual cross-resource
capture, secure destination provisioning/publication, scheduling or full business
restore; the installer must enforce those boundaries before first-user acceptance.

The protected Windows package destination is actually merged through
[PR307](https://github.com/Marachert/FinancialAssistant.Host/pull/307), actual
`938ba7d3af08a015ab22178e497fe9da79cfec61` at `2026-11-04T10:10:10Z`, final
`02f0a83d0bfb63a76c0b2f55bb872dfe2a29b951`, all five exact-head checks/re-review
clear. The valid abandoned-temporary-file finding was fixed, tested and resolved
with evidence after green. Full local1557/1557 and final scoped105/105 remain
separate verification results. The destination reuses native file ACL/link/lease checks,
flushed atomic no-overwrite writes and readback verification. Identical retries
are idempotent; conflicts/corruption are preserved and rejected. This does not
provision roots, capture source state, schedule retention or qualify installed
restore. Existing native-file constructor/default limits remain unchanged.

The coordinated capture component is actually merged through
[PR308](https://github.com/Marachert/FinancialAssistant.Host/pull/308), actual
`e469e2b1bd7c27046078a4656af0802cfd850ead` at `2026-11-04T20:39:29Z`, final
`5b1ac33fa8189f7f5f05b6d2ae6dabac70ff01d9`. All five exact-head checks and configured
re-review passed; final decoded CI verified1,614 tests across26 projects, no failures
or skips. A test-only TLS fixture repair resolved an initial CI failure without
changing production limits. This component orders a trusted exclusive source boundary,
ingress/scheduled-work freeze, broker drain, writer stop and repeated complete
quiescence checks around every required resource and package construction. It
clears transferred plaintext and rejects incomplete, stale, foreign or drifting
evidence. Concrete SCM/broker/SQL/object capture adapters and durable journal
recovery are still FIN-278/280 integration, not implemented by the orchestration
interface or certified by synthetic tests. No automatic writer resume or first-user
readiness is inferred. See the [capture contract](../engineering/windows-native-recovery.md#coordinated-capture-boundary).

An additional actual isolated Profile/Category restore scope now verifies portable
key access/wrong-key denial, a new empty target, exact preferences/taxonomy/events/
deletion-state hashes, independent roles and crash/restart with verified cleanup.
It is separate from the Income/Expense drill, not one atomic four-owner backup
or full13-owner capture. FIN-209 and installed/first-user acceptance remain open.

The 2026-09-15 [Windows-native deployment decision](windows-native-poc.md)
supersedes the Compose POC target. FIN-270 is implementation work: no WPF
installer or accepted native Windows runtime is claimed by this document.

| Area | Implemented repository baseline | Separate target or runtime gate |
| --- | --- | --- |
| Gateway | REST routing, JWT/role checks, exact public allowlist, trusted context, rate limits and safe errors | Route activation and destinations are environment-controlled; not every handler is enabled by repository defaults |
| Native HTTPS perimeter | FIN-207 [opt-in direct Kestrel TLS contract](../engineering/windows-native-https.md), pinned machine-certificate/private-key policy, header/Host/private-endpoint boundaries and free loopback TLS tests | FIN-207 remains open: FIN-278 integration, actual service certificate/firewall and approved-host acceptance are pending; no completion credit or public exposure |
| Native protected configuration | FIN-208 [typed immutable plans, machine-DPAPI packages, read-only service-bound artifact reader and mandatory SCM loading](../engineering/windows-native-protected-configuration.md), with conservative rotation preflight | FIN-208 remains open: privileged atomic activation, coordinated rotation, first-admin bootstrap, WPF integration, portable recovery and approved-host qualification are pending; no completion credit |
| Native portable recovery | FIN-209 [authenticated portable artifacts/key wrapping and actual Income/Expense restore drill](../engineering/windows-native-recovery.md), with wrong-key denial and crash/restart business verification | FIN-209 remains open: complete cross-owner backup, receipt objects/configuration, scheduling, installer integration and approved-host qualification are pending; no completion credit |
| Identity/Profile/Category | Deterministic authentication/session/provider-validation handlers, profile preferences and taxonomy | Stable secrets, durable stores and real provider configuration; phone delivery adapter is disabled by default |
| Intake/Receipt/AI | Editable drafts, review/reject/confirm, receipt/OCR processing and controlled provider abstractions | Approved provider budgets, privacy configuration and end-to-end hosted operation |
| Income/Expense | Separate native PostgreSQL authoritative records, atomic minimized outbox, immutable confirmation replay and lifecycle contracts | Financial delivery FIN-276 and scoped database recovery FIN-286 verified; installed and full backup/restore acceptance remain |
| Summary/Analytics | Separate owned PostgreSQL projections, strict replay and scoped generation recovery; deterministic dashboard composition | Financial Analytics delivery verified in FIN-276; Summary/remaining integration, settings-only refresh and installed acceptance remain |
| Financial Score | Owned PostgreSQL projections, deterministic score factors/history, neutral first reads and atomic immutable outbox | Financial source/settings delivery verified in FIN-276; settings-only refresh and integrated/installed acceptance remain |
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
not implied by the financial-only FIN-276 proof. FIN-283 adds [separate Profile/Category PostgreSQL
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
separate from installed acceptance after FIN-276. An Analytics-stage refresh handoff alone is not completed Score
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
Confluence full bodies were verified. At FIN-289 closure, local progress was195/215(90.7%),P919/39(48.7%),
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
required for FIN-270; their scoped database verification is recorded by FIN-286,
without waiving installed, backup or first-user gates.

## End-To-End User Scenario

FIN-277 adds [protected native adapters](../engineering/windows-native-adapters.md):
restart-safe encrypted receipt objects with ACL/path/key/retention controls,
disposable bounded service-health observations and a private technical diagnostic
sink. FIN-285 native Receipt metadata fails closed when owned storage is invalid; native reliable
delivery outside the confirmed financial path remains an integration gate. The local sink does not qualify retained native collectors,
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
