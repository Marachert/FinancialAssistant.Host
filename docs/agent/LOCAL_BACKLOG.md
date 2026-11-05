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
| FIN-276 | Verified Merged; Jira reconciliation pending | [PR295](https://github.com/Marachert/FinancialAssistant.Host/pull/295) actual `d142b075dced5847da8f12f9d237763e50d06e2c` at `2026-11-02T00:29:46Z`, final `2a78ae2c35c6a83218892073a65fff8226a770e8`, all five exact-head checks green, configured re-review clear and sixteen post-merge Confluence full bodies verified. Native financial event delivery and actual crash/replay, not installed or first-user acceptance. |
| FIN-286 | Verified Merged; Jira reconciliation pending | [PR297](https://github.com/Marachert/FinancialAssistant.Host/pull/297) actual `d1704ab108892d8ee7007633066d5d9a21d751d5` at `2026-11-02T04:57:18Z`, final `bf9326a7b2eba1c403c3a7c2af2f2cebb1ed5f7c`, all five exact-head checks green and configured review clear. Full1286/1286, [all nine actual owned database scenarios and separate native-file recovery](../engineering/native-persistence-qualification.md) passed; nineteen post-merge Confluence full bodies verified. Not installed or first-user acceptance. |
| FIN-207 | In Progress; implementation merged, qualification pending | [PR299](https://github.com/Marachert/FinancialAssistant.Host/pull/299) independently verified merged=true/CLOSED: actual protected squash `f3e117ce127d4f204bb2a88d568badaf6ad3e358` at `2026-11-02T14:30:58Z`, final `d057950852f78b163bef5ca913384c38fa1431d0`; all five exact-head checks passed (Backend41460084729/Mobile41460084524/Admin41460084647), re-review completed `2026-11-02T14:18:08.465944Z` with no findings. One valid finding was reproduced, fixed, regressed, answered with commit/test/CI evidence and resolved only after green; final channels/labels/scope/mergeability were clear. [Direct Kestrel HTTPS contract](../engineering/windows-native-https.md), full1344/1344 across25 projects, zero failures/skips, Gateway80/80, Hosting102/102; full baseline build0errors/5pre-existing warnings, review-fix scoped build0warnings/0errors, format/privacy1303/Markdown158/574/manifest passed. Ten affected Confluence full bodies verified; the actual-merge snapshot was verified at Delivery157/Bootstrap546. FIN-278 integration and approved-host acceptance remain required: FIN-207 is NOT Done and adds no closure credit. No public exposure, certificate purchase, machine installation or additional spending. |
| FIN-208 | In Progress; runtime component merged, integration/qualification pending | [PR301](https://github.com/Marachert/FinancialAssistant.Host/pull/301) independently verified merged=true/CLOSED: actual protected squash `63fe6b12e8ba2e9cd6f7e24a16b52c91cf74cfb6` at `2026-11-02T20:45:37Z`, final `b2072ab474616f6eb854619555d66da28814e7ea`. All five exact-head checks passed (Backend41519403037/Mobile41519402763/Admin41519402719); configured re-review completed `2026-11-02T20:30:12.075428Z` with no findings. The valid missing-baseline P1 was reproduced by six regressions, fixed, evidenced in reply4447399430 and resolved only after green. [Typed protected configuration, Windows DPAPI packages, service-bound read-only artifact loading, mandatory SCM configuration and conservative rotation preflight](../engineering/windows-native-protected-configuration.md); final full1451/1451 across25 projects, zero failures/skips, Hosting209/209, Gateway80/80. Nine final pre-merge Confluence full bodies verified; central actual-merge snapshots verified at Delivery164/Bootstrap552. Privileged atomic activation, first-admin bootstrap, coordinated credential rotation, FIN-278/279 integration, FIN-209 portable recovery and approved-host qualification remain mandatory. Machine-bound protection alone is not a backup. No paid provider, machine installation or closure credit. |
| FIN-209 | In Progress; portable component merged, complete recovery/integration pending | [PR303](https://github.com/Marachert/FinancialAssistant.Host/pull/303) independently verified merged=true/CLOSED: actual protected squash `379e6fc630b62aa0f1f269188db89c3e45ac66bd` at `2026-11-03T08:21:44Z`, final `4515a8229b89a43a0e94866d2972369b84df8237`. All five exact-head checks passed (Backend41600143931/Mobile41600143848/Admin41600143814); configured review completed `2026-11-03T08:03:10.597513Z` without findings, all final channels/labels clear. [Portable authenticated artifacts/key wrapping and actual owned Income/Expense restore drill](../engineering/windows-native-recovery.md); full1484/1484 across26 projects, zero failures/skips, real wrong-key denial and crash/restart business reconciliation with verified cleanup. Nine pre-merge Confluence full bodies verified. All-owner consistency, receipt/configuration capture, authenticated complete manifests, broker/search recovery, retention/scheduling, update/repair preservation, FIN-278/280 integration and approved-host qualification remain mandatory. Same-cluster synthetic two-owner proof is not complete POC backup; no Done or completion credit. No production/shared restore, installation or additional spending. |
| FIN-278 | Pending | Restart-safe privileged installation engine; prerequisite ordering must preserve security/configuration/recovery requirements. No SCM installation on this working PC. |
| FIN-279 | Pending | WPF installation/maintenance wizard. |
| FIN-280 | Pending | Qualified versioned packaging, repair/update/safe uninstall, integrity/notices/support checks. Do not replace package trust checks with paid purchases or silent bypasses. |
| FIN-281/206 | Pending owner-run qualification | Reproducible Windows lifecycle harness and actual installed-host acceptance. Mark unavailable environments/tests pending, not passed. |

FIN-270 remains open locally with15/19 verified complete; last verified Jira snapshot had11/19 Done.
FIN-271/272/273/274/282/283/275/284/277/285/287 were verified Done. A parent cannot
close merely because child code exists; its integrated acceptance still applies.

FIN-276 is verified merged through PR295. Its reviewed scope includes durable
native broker/outbox/inbox delivery through Analytics, Score, Recommendation
preparation and minimized Audit, with authorized Profile input and private order.
Full1282/1282 tests and actual message-specific replay/restart passed; all owned
fixture processes stopped. See the [lifecycle guide](../engineering/native-lifecycle-delivery.md).
FIN-286 now verifies the migration/retention/recovery crosswalk across all nine
database scenarios and thirteen business owners. FIN-207 implementation PR299 and
its checkpoint PR300 are actually merged; FIN-207 qualification stays open. The
FIN-208 runtime component is also actually merged; its integration/qualification
stays open. The next independent prerequisite is FIN-209 before installer work.
FIN-209 now has a [bounded portable recovery component and actual two-owner
restore drill](../engineering/windows-native-recovery.md): local full1484/1484,
wrong-key denial, exact financial reconciliation/replay and real crash/restart
passed. This is not whole-POC backup or FIN-209 closure; integrated coverage and
qualification remain open, without additional completion credit.
The manifest component is actually merged through [PR305](https://github.com/Marachert/FinancialAssistant.Host/pull/305):
protected squash `edc4a76155d68ebed032bbca06845de7b810c000` at
`2026-11-03T08:57:26Z`, final `5dc07721b8dcef2bccd8a7228ed0810e414481d7`, all five
exact-head checks green (Backend41606135388/Mobile41606135402/Admin41606135375),
configured review completed `2026-11-03T08:50:30.176799Z` without findings and
final channels clear. It supplies bounded authenticated
inventory validation for 13 databases, 16 configurations including Summary and one
receipt-object bundle. Metadata coverage is not actual quiescence/capture or
whole-application restore. No Done or extra completion credit.
Local follow-on full1528/1528 across26 projects, recovery75/75, zero failures/skips;
this is manifest metadata/artifact validation, not another actual business drill.
The package component is actually merged through [PR306](https://github.com/Marachert/FinancialAssistant.Host/pull/306),
actual `ce26f8344982b884e2f5ba0cb4c01fc1b6fd5ce6` at `2026-11-03T09:35:20Z`, final
`ab4297866f02158bd4ee8d1573e68bdab7e74723`, all five exact-head checks green
(Backend41610686585/Mobile41610686653/Admin41610686645), configured review clear
at `2026-11-03T09:25:17.152848Z` and final channels clear. It supplies encrypted package construction,
caller-owned stream writing and independent all-artifact authentication, including
owned file/failure tests. Actual quiescence/capture, destination provisioning,
atomic publication, scheduling and installer/business acceptance remain open.
The protected destination component is actually merged through
[PR307](https://github.com/Marachert/FinancialAssistant.Host/pull/307), actual
`938ba7d3af08a015ab22178e497fe9da79cfec61` at `2026-11-04T10:10:10Z`, final
`02f0a83d0bfb63a76c0b2f55bb872dfe2a29b951`. All five exact-head checks passed
(Backend41810861267/Mobile41810861173/Admin41810861214), configured review clear
at `2026-11-04T09:58:20.226277Z`. The valid abandoned-temporary-file finding was
fixed, regressed, answered with commit/test/CI evidence and resolved after green.
Final channels/labels were clear; five affected Confluence bodies and central
post-merge Delivery182/Bootstrap570 were verified. Full local baseline1557/1557
and fresh review-fix scoped105/105 are separate results, not a new combined count.
The original12MiB helper default/constructor remain unchanged; this consumer
explicitly opts into128MiB. Private existing NTFS roots, atomic no-overwrite saves,
identical retries, persisted-byte checks and authenticated opens preserve existing
archives; startup removes only validated abandoned temporary files under the lease.
The coordinator is actually merged through
[PR308](https://github.com/Marachert/FinancialAssistant.Host/pull/308), actual
`e469e2b1bd7c27046078a4656af0802cfd850ead` at `2026-11-04T20:39:29Z`, final
`5b1ac33fa8189f7f5f05b6d2ae6dabac70ff01d9`, all five checks green
(Backend41816672625/Mobile41816672779/Admin41816672565), configured review clear
at `2026-11-04T10:51:01.849646Z`, final channels/labels clear and actual-merge
Confluence Delivery185/Bootstrap573 verified. Final decoded CI1614/1614 across26
projects is distinct from local baseline1613 and final scoped Recovery161/Gateway80.
The initial CI failure remains failed; a minimal test-only TLS fixture repair
passed fresh fixing-head CI without changing production limits. This is
ordered orchestration with a trusted privileged adapter contract, not implemented
SCM/broker/SQL capture actions or actual all-owner business restore. Root provisioning,
actual capture, scheduling and integrated/installed acceptance remain open;
no Done or completion credit.
That FIN-209 increment used `codex/fin-209-profile-category-restore` from the
then-current main. A separately selected Profile/Category owned restore drill passed
initial and actual immediate-crash/restart phases with cleanup verified at
`2026-11-04T21:02:14.3977960Z`. It proves recovered key access/wrong-key denial,
new-empty-target restore, exact preferences/category/outbox/deletion snapshots
and independent role denial, identical recovered-event replay and conflicting
replay rejection without changing pending-event/business hashes. This is not one atomic four-owner boundary with the
Income/Expense drill, complete13-owner recovery, installed acceptance or a closure.
The Profile/Category increment is actually merged through
[PR309](https://github.com/Marachert/FinancialAssistant.Host/pull/309), actual
`53c0a3f41ce20c871a1a8231a596ac3c00eff9f4` at `2026-11-04T21:28:32Z`, final
`4f08be7332c4050c59e35f68d1440e19512e1540`; all five checks green
(Backend41914728090/Mobile41914728014/Admin41914728194), decoded CI1617/1617 across26,
review clear at `2026-11-04T21:18:59.878229Z`, final channels/labels clear and
post-merge Confluence Delivery188/Bootstrap576 verified. No closure credit.
That Identity increment used `codex/fin-209-identity-restore` from the then-current main.
The Identity-only owned recovery scope passed initial and actual immediate-crash/
restart phases with cleanup at `2026-11-04T21:42:54.5522512Z`. It captures compatible
synthetic source configuration/data keys, checks account/session/challenge/outbox
state and duplicate/conflicting replay denial, and validates a real native sign-in
and JWT inside an explicitly rolled-back transaction. Google/Apple/phone remain
disabled and events pending transport. This is not real DPAPI export, replacement-
host provisioning, one atomic five-owner boundary, complete backup or installed
acceptance. Remaining all-owner consistency/integration gates stay open.
Local full1618/1618 across26 projects, zero failures/skips, passed through
`2026-11-04T22:04:26.4849056Z`; normal Identity and both earlier recovery scopes
also passed with cleanup. This is not a new closure or exact-head CI claim.
The Identity increment is actually merged through
[PR310](https://github.com/Marachert/FinancialAssistant.Host/pull/310), actual
`4f60aef945c09278fcdb86f63ce99583aea6687d` at `2026-11-04T22:50:26Z`, final
`2d01458b2243b958f44d4a86b40b3a1670296cb6`, all five exact-head checks green
(Backend41922585385/Mobile41922585390/Admin41922585560), decoded CI1618/1618 across26,
review clear at `2026-11-04T22:09:51.055926Z`, final channels/labels clear and
post-merge Confluence Delivery191/Bootstrap579 verified. No closure credit.
That Intake increment used `codex/fin-209-intake-restore` from the then-current main.
The explicit Intake scope captures its three owned synthetic schemas and key sets
together: Transaction Intake, Income and Expense. Exact drafts/confirmations,
financial and bounded complete outbox-row hashes, owner/currency/role isolation,
replay/conflict denial and expired-source absence are required before and after
actual crash/restart. This is not complete13-owner backup or installed acceptance;
the actual three-owner restore/crash/restart drill passed with cleanup at
`2026-11-04T22:58:28.8772565Z`. Normal Intake and all three previous recovery scopes
passed with cleanup. Local full1619/1619 across26 projects, zero failures/skips,
finished `2026-11-04T23:17:39.3461300Z`; restore/Release build, full format and final
Ledger21 passed. Those local results were not themselves CI/review/merge acceptance.
The Intake increment is actually merged through
[PR311](https://github.com/Marachert/FinancialAssistant.Host/pull/311), actual
`b0045e0a24ddfdb7a23cdf382654ca102185c4c6` at `2026-11-04T23:33:56Z`, final
`177b3c6c45dd5e7a99d9d504fa6d5346b756ef26`. All five exact-head checks passed
(Backend41933146568/Mobile41933146592/Admin41933146549), decoded CI1619/1619 across26,
configured review clear `2026-11-04T23:23:33.864792Z`, final channels/labels clear,
post-merge Confluence Delivery194/Bootstrap582 verified. No closure credit.
That Audit increment used `codex/fin-209-audit-restore` from the then-current main.
Audit recovery must preserve all bounded entry/replay-receipt hashes, original
timestamps and minimum retention, revoked-admission historical reads, physical
expired-payload absence and immutable replay. Its empty Score schema exists only
for independent role-isolation/readiness checks, not Score business recovery.
The actual Audit restore/crash/restart passed with cleanup at
`2026-11-04T23:44:31.0064620Z`. Normal Audit and all four earlier recovery scopes
also passed with cleanup after the scope-table refactor. Local full1620/1620 across26
projects, zero failures/skips, finished `2026-11-05T00:05:17.2758050Z`; full restore/
Release build, format0/1299 and Ledger21 passed. These were local results only.
The Audit increment is actually merged through
[PR312](https://github.com/Marachert/FinancialAssistant.Host/pull/312), actual
`f6a00e883ecda69f4716b532a398dd8374a578d8` at `2026-11-05T00:22:41Z`, final
`67428cab193fd86c0bd77c46a1e8b54cc4fcc550`, all five checks green
(Backend41939225637/Mobile41939225893/Admin41939225658), decoded CI1620/1620 across26,
review clear `2026-11-05T00:10:55.447674Z`, final channels/labels clear and
post-merge Confluence Delivery197/Bootstrap585 verified. No closure credit.
FIN-209 now continues on `codex/fin-209-projection-restore` from that actual main.
The separate Summary/Analytics restore/crash/restart passed with cleanup at
`2026-11-05T00:53:07.5112343Z`: all stored records match their source digests before/
after replay, both rebuilds retain100 staged records, pending publications remain
unacknowledged and exact totals/owner/role/key isolation pass. The native bounded
read-only inspector reuses the production outbox decoder without cursor writes.
Earlier failed fixture/extraction attempts remain failed. Normal Projections and
all five earlier recovery scopes passed with cleanup. Local full1621/1621 across26
projects, zero failures/skips, passed through `2026-11-05T01:25:32.4367174Z`;
restore/Release build passed with zero errors/five existing warnings. Exact-head
CI/review/protected merge remain required. No complete POC backup is claimed.
No pending installed qualification is silently waived to advance. Native first-user
bootstrap, Profile-only refresh and protected machine deployment remain explicit
launch gaps, not inferred acceptance.

## Progress And Evidence

Current local verified completion is **197/215, 91.6%**, P9 **21/39, 53.8%**;
first-user **Not Ready**. Increase from FIN-286 is100/215=0.4651percentagepoints,
reported+0.5pp before rounding. This is **not a new live Jira census**. See
[structured actual-merge evidence](LOCAL_DELIVERY_EVIDENCE.json) and
[the progress snapshot/history](POC_PROGRESS.md). No scope denominator or duplicate
rule changed; FIN-288, FIN-289, FIN-276 and FIN-286 each receive one unique local completion credit.
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
- FIN-276: reconcile actual PR295 scope/merge/verification evidence when Jira returns.
- FIN-288, FIN-289 and FIN-276 are locally verified complete during the outage. Preserve
  the separate stale Jira baseline and do not double-count it after reconciliation.
- Preserve prior actual merge/closure history and canonical duplicate rules.
