# Temporary POC Backlog

Owner-approved 2026-10-29: continue no-cost POC development using repository-local
tracking until Jira is restored. Jira currently returns `SUSPENDED_INACTIVITY`;
do not pay to unblock it, create a replacement site or fabricate Jira transitions.
Scope below comes from the last verified Jira snapshot and repository evidence,
not a claim that suspended Jira was read successfully today.

## Delivery Rules

- Restore GitHub/main/PR/CI/reviews, Confluence, local files and included usage.
  Hold the owned delivery lock during work; preserve all existing user changes.
- Resume the single open agent PR first. Otherwise follow the dependency order
  below, reading cached scope and source/tests before implementation.
- Continue the existing FIN-key branches and PR titles. A local item becomes
  In Progress when ownership is recorded here; becomes Verified Merged only after
  actual protected GitHub merge, exact-head green CI, clear review channels,
  scoped verification and post-merge Confluence/documentation evidence.
- The local backlog/evidence replaces unavailable Jira-only gates temporarily.
  All other merge gates remain. No direct main push, force-push or protection bypass.
- After each verified leaf merge, update the POC progress snapshot/history by a
  guarded ledger PR before the next product item. Label local verified completion
  and stale Jira status separately. Do not present a local closure as Jira Done.
- Reconcile cached scope, links, actual merge/test evidence and pending transitions
  with live Jira before returning to its normal workflow. Conflicts require review;
  never overwrite newer owner edits or double-count work.

## First Working POC

Prioritize a local Windows path through sign-in, explicit transaction/draft review,
confirmation, durable income/expense state and usable balances/dashboard. WPF
native installation remains the agreed target; no Docker/WSL or machine mutation
on the owner's working PC is implied. Prepare reproducible owner-run instructions.
Financial correctness, access control, durable recovery and honest readiness are
not optional refinements. A running process or memory-only demo is not acceptance.

External paid AI/OCR, push providers, hosted infrastructure, paid CI, commercial
features, purchased certificates and app-store publication are excluded from agent
work. Use existing manual-review/deterministic/local fallbacks and in-app state;
show unavailable capabilities honestly. No paid trial, API-billed fallback,
credit/reset redemption, real-provider send or additional spending. Later optional
improvements must not delay independent free POC implementation.

## Active Work

| Key | Local state | Scope and dependencies |
| --- | --- | --- |
| FIN-288 | Verified Merged; Jira reconciliation pending | PR291 actual `c4e6a9e15ab2602aa1d68fc45b48bf4899634e60`, merged `2026-10-29T10:11:34Z`; final `5b9a5c04571011cbb86b2fc9b4145d7e9a89d09a`, all five CI green, completed review no findings, post-merge Confluence verified. Closure PR292 also verified merged as `c46b2ed5d313469d83e8b6c2e4da2c968318f94f` before FIN-289 began. |
| FIN-289 | Verified Merged; Jira reconciliation pending | [PR293](https://github.com/Marachert/FinancialAssistant.Host/pull/293) actual `f870733b35bc1d74a4248f882d4426faeef1e92e`, merged `2026-10-29T13:53:43Z`; final `77adda433b0e8f3d58c6495a50bc34617615326d`, all five CI green, both valid findings fixed/tested/evidenced/resolved after green, re-review no findings and nine post-merge Confluence bodies verified. Native minimized Audit metadata/replay/retention, not live transport or installed acceptance. |
| FIN-276 | In Progress, local ownership | Real durable local RabbitMQ/inbox/outbox/consumer convergence and authorized settings ingestion; synthetic direct calls are not delivery proof. Branch `codex/fin-276-native-lifecycle` from actual closure main `bd1560d40befa91f14d9469032c26c43dbe70e3d`. Cached leaf/parent/all19 children and prerequisites were restored; FIN-288/289 are verified locally. Implementation is being published through its single guarded PR; no merge or new completion credit is inferred. |
| FIN-286 | Pending, depends on FIN-276 | Full native persistence/migration/retention/crash recovery integration, including all previously merged owners. |
| FIN-207/208/209 | Required dependency work | Local HTTPS, protected secrets and verified backup/restore. Restore cached descriptions/source before starting; no purchased certificate required to develop free test paths. |
| FIN-278 | Pending | Restart-safe privileged installation engine; prerequisite ordering must preserve security/configuration/recovery requirements. No SCM installation on this working PC. |
| FIN-279 | Pending | WPF installation/maintenance wizard. |
| FIN-280 | Pending | Qualified versioned packaging, repair/update/safe uninstall, integrity/notices/support checks. Do not replace package trust checks with paid purchases or silent bypasses. |
| FIN-281/206 | Pending owner-run qualification | Reproducible Windows lifecycle harness and actual installed-host acceptance. Mark unavailable environments/tests pending, not passed. |

FIN-270 remains open locally with13/19 verified complete; last verified Jira snapshot had11/19 Done.
FIN-271/272/273/274/282/283/275/284/277/285/287 were verified Done. A parent cannot
close merely because child code exists; its integrated acceptance still applies.

FIN-276 working evidence: focused Messaging73/Audit45/Income20/Expense20/
Profile39/Analytics52/Score33/Hosting60/Recommendations53 tests passed. Actual
Profile, projection-order, recommendation preparation/preferences and bounded
31-owner outbox PostgreSQL restart proofs passed. Three actual broker transport
runs qualified retries/durable recovery/hosted reconnect with cleanup. Combined
automatic financial convergence, PostgreSQL immediate-crash/restart, pending
broker/outbox recovery and actual acknowledged duplicate replay passed in
`201b6d1fcec144048034d392687a467c` at `2026-10-29T22:28:07Z`. The promoted
repository runner independently passed `facb5713df134d088c03bbd2b8626537` at
`2026-10-30T03:48:01Z`; both verified cleanup and zero manual projector/provider
calls. An initial promoted-run listener timeout also cleaned up; the approved
outside-sandbox retry passed. No current-head CI, review, merge, installed or
first-user acceptance is inferred. Full verification and deployment/settings-only
coverage remain pending; no extra feature credit. Confluence Delivery138/
Bootstrap527 snapshots predate the combined pass. Delivery139/Bootstrap528 and
nine affected mapped pages were subsequently verified in full with actual flow
evidence. Final local full1282/1282 and format0/1250 passed; promoted transport
retry/restart/reconnect also passed, cleanup verified at2026-10-30T04:21:18Z. See the
[local lifecycle guide](../engineering/native-lifecycle-delivery.md).

The later full1252/1252 result passed across25 projects (four existing build cache
warnings, zero errors/failures/skips), before the new native Audit adapter. That
adapter separately passed45/45 focused tests and a clean scoped build. It maps
broker producer identity to the unchanged Audit admission/transaction boundary;
no combined transport/storage acceptance or completion credit is inferred.

## Progress And Evidence

Current local verified completion is **195/215, 90.7%**, P9 **19/39, 48.7%**;
first-user **Not Ready**. Increase from FIN-289 is100/215=0.4651percentagepoints,
reported+0.5pp before rounding. This is **not a new live Jira census**. See
[structured actual-merge evidence](LOCAL_DELIVERY_EVIDENCE.json) and
[the progress snapshot/history](POC_PROGRESS.md). No scope denominator or duplicate
rule changed; FIN-288 and FIN-289 each receive one unique local completion credit.
This maintenance adds none.

Last verified published census: **193/215, 89.8%**, P9 **17/39, 43.6%**;
first-user **Not Ready** at that historical snapshot. Subsequent FIN-288/289 local
credits are recorded separately above; this tracking document adds no feature credit.
See [the progress history](POC_PROGRESS.md).
This is not a new live Jira census.

FIN-289 local closure2026-10-29: actual PR293 merge/final head are recorded above.
All five final-head checks passed (Backend40883169464, Mobile40883169618,
Admin40883169414); re-review completed13:36:39.395023UTC with no findings. Timeout
and historical-admission findings were fixed, tested, replied4413120306/4413122660
and resolved only after green. Full1128/1128 baseline and final30/30 scoped Audit
regressions passed, not an inferred second full suite. Actual isolated PostgreSQL
initial/immediate-crash/restart cleanup verified13:30:10UTC, including revoked
producer/removed-class historical reads and31 physical expiry deletions; installed
false/releaseReadyfalse. Final changed-source format, privacy1228 and Markdown155/514
passed. Nine post-merge Confluence full-body readbacks verified. This guarded ledger
publishes195/215 without fabricating Jira Done or first-user acceptance.

Closure PR294 was independently verified merged=true/CLOSED as
`bd1560d40befa91f14d9469032c26c43dbe70e3d` at `2026-10-29T15:14:41Z`, final
`25f3fe290ca6ba7683193f077299126b9920afe2`, all five checks green
(Backend40897270706, Mobile40897270848, Admin40897271035), review no findings and
clear final channels. Nine final Confluence current bodies and eight additional
mapped architecture/storage/index/workflow pages were fully read-back verified.
FIN-276 started only after that publication and fresh main/duplicate/scope recovery.

FIN-288 local verification increment 2026-10-29: the expanded combined owned
PostgreSQL Score and Recommendations/Notifications fixture passed initial and
immediate-crash/restart phases; cleanup verified `2026-10-29T09:17:06Z`,
installed=false/releaseReady=false. Separate roles, rollback, concurrent replay,
lifecycle/preferences, Score input, trigger deduplication and immutable outbox
acknowledgements were exercised. Unit results: Score 29/29 and Recommendations/
Notifications 41/41. Full restore and Release build passed (zero errors, five
existing warnings); all 1119 tests across 24 projects passed, none skipped.
Markdown 154 files/500 links and the unqualified native manifest passed.
Full formatting verified zero changes across 1170 files; staged privacy 1216 passed.
Confluence Delivery, Bootstrap, Home, Overview, Architecture, Catalog, Release
and Testing pages were updated with complete-body readback verification.
All five exact-head checks passed (Backend40853974694, Mobile40853974669,
Admin40853974692); configured review completed10:01:35UTC with no findings,
final channels clear. Protected expected-head squash and independent post-merge
read verified actualc4e6a9e15ab2602aa1d68fc45b48bf4899634e60 at10:11:34UTC.
Eight post-merge Confluence full-body readbacks passed. Local FIN-288 closure and
its guarded ledger PR292 are verified. Actual closure merge
`c46b2ed5d313469d83e8b6c2e4da2c968318f94f` at `2026-10-29T10:51:04Z`, final
`7cc75cb204158b31d447bdfca0b18e80469e3ca7` all five CI green, completed review no
findings; all eight final Confluence closure-ledger readbacks verified. FIN-289
started only after this publication and fresh state recovery.

FIN-287 implementation PR289 actually merged
`e6b8f6abc8dac24911eafda65f6515a7feed384a` at `2026-10-20T21:12:28Z`.
Closure ledger PR290 actually merged `93ff1ef01988789e49b78068afcd0b0cd6a15553`
at `2026-10-21T04:34:40Z`. Final ledger head
`aa49e380a88c119b1d69efd79669e0548d87c839` had all five checks green, both valid
findings fixed/evidenced/resolved after green and completed re-review with no
further findings. All 27 affected Confluence post-merge readbacks were verified.

## Pending Jira Reconciliation

- FIN-288: verify live scope/ownership and reconcile the actual PR291 evidence;
  it was last verified In Progress. No Jira Done transition is claimed.
- FIN-289: verify live scope/ownership and reconcile actual PR293 and its reviewed
  tests/CI/Confluence evidence. No Jira transition is claimed during suspension.
- FIN-288 and FIN-289 are newly locally verified complete during the outage. Preserve
  the separate stale Jira baseline and do not double-count it after reconciliation.
- Preserve prior actual merge/closure history and canonical duplicate rules.
