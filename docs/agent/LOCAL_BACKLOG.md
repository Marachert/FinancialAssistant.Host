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
| FIN-288 | In Progress, unmerged implementation candidate | Separate Score and Recommendations/Notifications durable state, settings/preferences, deterministic factors/history, lifecycle and atomic outbox. Last verified prerequisites FIN-275/283/287 Done. Current branch `codex/fin-288-native-insights`, base `93ff1ef01988789e49b78068afcd0b0cd6a15553`; exact-head CI/review/merge remain required. |
| FIN-289 | Pending | Append-only minimized Audit persistence/retention. Last verified prerequisite FIN-274 Done. |
| FIN-276 | Pending, depends on FIN-288/289 | Real durable local RabbitMQ/inbox/outbox/consumer convergence, authorized settings ingestion; synthetic direct calls are not delivery proof. |
| FIN-286 | Pending, depends on FIN-276/288/289 | Full native persistence/migration/retention/crash recovery integration, including previously merged owners. |
| FIN-207/208/209 | Required dependency work | Local HTTPS, protected secrets and verified backup/restore. Restore cached descriptions/source before starting; no purchased certificate required to develop free test paths. |
| FIN-278 | Pending | Restart-safe privileged installation engine; prerequisite ordering must preserve security/configuration/recovery requirements. No SCM installation on this working PC. |
| FIN-279 | Pending | WPF installation/maintenance wizard. |
| FIN-280 | Pending | Qualified versioned packaging, repair/update/safe uninstall, integrity/notices/support checks. Do not replace package trust checks with paid purchases or silent bypasses. |
| FIN-281/206 | Pending owner-run qualification | Reproducible Windows lifecycle harness and actual installed-host acceptance. Mark unavailable environments/tests pending, not passed. |

FIN-270 remains open locally; last verified Jira snapshot had 11/19 children Done.
FIN-271/272/273/274/282/283/275/284/277/285/287 were verified Done. A parent cannot
close merely because child code exists; its integrated acceptance still applies.

## Published Baseline

Last verified published census: **193/215, 89.8%**, P9 **17/39, 43.6%**;
first-user **Not Ready**. No completion credit for unfinished FIN-288 or this
temporary tracking document. See [the progress history](POC_PROGRESS.md).
This is not a new live Jira census.

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
Exact-head CI/review/actual merge and final publication remain required;
no additional completion credit yet.

FIN-287 implementation PR289 actually merged
`e6b8f6abc8dac24911eafda65f6515a7feed384a` at `2026-10-20T21:12:28Z`.
Closure ledger PR290 actually merged `93ff1ef01988789e49b78068afcd0b0cd6a15553`
at `2026-10-21T04:34:40Z`. Final ledger head
`aa49e380a88c119b1d69efd79669e0548d87c839` had all five checks green, both valid
findings fixed/evidenced/resolved after green and completed re-review with no
further findings. All 27 affected Confluence post-merge readbacks were verified.

## Pending Jira Reconciliation

- FIN-288: verify its live status/description/ownership after restoration; it was
  last verified In Progress. No actual merge or Done transition is claimed.
- No new completed leaf or backlog percentage is claimed during the outage yet.
- Preserve prior actual merge/closure history and canonical duplicate rules.
