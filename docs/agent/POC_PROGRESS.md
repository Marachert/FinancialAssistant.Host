# POC Readiness Progress

Last updated: 2026-10-11

## Current Snapshot

POC backlog completion after FIN-283 native Profile/Category closure is **88.7%**:
**188 of 212 canonical POC leaf tickets are Done**.

The fresh 2026-10-11 18:04:51 UTC census contains 286 unique Jira issues and
confirms 188/212 after actual PR278 merge and FIN-283 Done. This is +0.5 percentage
points calculated before rounding (100/212 = 0.4717 pp), from 187/212 (88.2%).
FIN-270 remains In Progress with 6/16 children Done; FIN-38 P9 is 12/36 (33.3%).
FIN-283 delivers separate native Profile/Category PostgreSQL adapters, exact
preferences/catalog rehydration, owner-isolated concurrent updates, atomic
Category/outbox and replay-safe deletion markers. It does not deliver live broker,
installer, full cross-service privacy erasure or installed-Windows acceptance.
FIN-284 is the next ranked leaf after this closure ledger is merged and its
dependencies are read fresh. First-user testing is Not Ready; no calendar ETA is
inferred from the percentage.

Historical FIN-282 Identity closure snapshot:

The fresh 2026-10-11 12:17:01 UTC census contains 286 unique Jira issues and
confirms 187/212 after actual PR276 merge and FIN-282 Done. This is +0.5 percentage
points calculated before rounding (100/212 = 0.4717 pp), from 186/212 (87.7%).
FIN-270 remains In Progress with 5/16 children Done; FIN-38 P9 is 11/36 (30.6%).
FIN-282 delivers native Identity PostgreSQL accounts/session/challenge/outbox
adapters, atomic owned mutations and restart-safe retention/deletion, not broker,
installer or installed-Windows acceptance. FIN-283 is the next ranked leaf after
this closure ledger is merged and dependencies are read fresh. First-user testing
is Not Ready; no calendar ETA is inferred from the percentage.

Historical FIN-274 foundation closure snapshot:

The fresh 2026-10-11 04:36:46 UTC census contains 286 unique Jira issues and
confirms 186/212 after actual PR274 merge and FIN-274 Done. This is +0.5 percentage
points calculated before rounding (100/212 = 0.4717 pp), from 185/212 (87.3%).
FIN-270 remains In Progress with 4/16 children Done; FIN-38 P9 is 10/36 (27.8%).
FIN-274 delivers the shared PostgreSQL foundation, not completed native domain
adapters or an installed POC. FIN-282 is the next ranked leaf after this closure
ledger is merged and dependencies are read fresh. First-user testing is Not Ready.

The fresh 2026-10-10 19:50 UTC census contains 286 unique Jira issues. FIN-274's
original multi-owner persistence work is split into a PostgreSQL foundation and
five sibling leaves FIN-282 through FIN-286 under FIN-270. All original capabilities
remain required; no new product features or completed work were added/removed.
The leaf-count denominator grows from 207 to 212: 185/207 (89.4%) becomes 185/212
(87.3%), a -2.1 percentage-point scope/granularity adjustment calculated before rounding, not a
delivery regression. At decomposition FIN-270 had 3/16 children Done and FIN-274
was the active foundation leaf; domain adapters and end-to-end persistence
verification remain separate unfinished siblings. First-user testing is Not Ready.

The historical FIN-273 closure census contains 281 unique Jira issues and confirms 185/207
after verified FIN-273 merge and Jira Done. This is a +0.5 percentage-point change
from 184/207 (88.9%), calculated before rounding. FIN-273 delivers Windows Service
hosting code for fifteen APIs, native routing/trust/readiness configuration and a
pure service/publish plan, not an installed POC. The owner explicitly approved the
code/free-verification versus installed-service phase split on 2026-10-10.
Actual SCM registration, recovery, reboot, least privilege, cleanup and the
three-Windows matrix remain mandatory FIN-278/281/final owner acceptance.
FIN-272's inventory/inspection and FIN-278/280 package/installation gates remain
separate. No gate is waived; first-user testing is Not Ready.

The 2026-09-15 scope census contains 281 unique Jira issues. FIN-270 added eleven
initially unfinished canonical Sub-tasks (FIN-271 through FIN-281); its parent Story is
not counted as a leaf. No delivered work was removed. The old 182/196 (92.9%)
becomes 182/207 (87.9%), a -4.9 percentage-point scope adjustment calculated
before rounding. This is not a delivery regression or an elapsed-time estimate.
The previous FIN-271 closure moved 182/207 (87.9%) to 183/207 (88.4%),
a +0.5 percentage-point closure after the separate scope adjustment.

Approved target: [Windows-native WPF installer](../architecture/windows-native-poc.md)
on Windows 11 and Server 2022/2025, without Docker/WSL. Identity and separate
Profile/Category adapters are merged;
the remaining domain adapters, broker, installer and installed-host acceptance
remain unfinished capabilities.

The POC is **not yet ready for first-user testing**. The percentage measures
completed backlog scope; it is not an estimate of elapsed time or a substitute
for the readiness gates below.

Latest canonical and Jira closure:

- FIN-283 - Persist native profile preferences and category catalogs
- implementation PR: https://github.com/Marachert/FinancialAssistant.Host/pull/278
- actual merge: https://github.com/Marachert/FinancialAssistant.Host/commit/dba7624e84bcfac7991fc41610e8ad829716e0e6
- merged at: `2026-10-11T18:01:35Z`
- final head: `246ec784ea061a468ad51b8906ca2604da591b1f`
- exact-head CI: Backend38161517454, Mobile38161517466, Admin38161517445;
  all five checks succeeded, no pending/cancelled/skipped checks
- previous/current completion: 187/212 (88.2%) to 188/212 (88.7%); +0.5 pp before rounding
- both null-owner and oversized-correlation findings fixed/regressed, replied with
  commit/test/CI evidence and resolved only after green; final channels clear
- local full baseline plus final scoped regression reruns: 955/955 across 24 projects,
  zero skips; Profile13/Category22/Persistence30/Hosting54/Repository315; final scoped
  and integration builds zero warnings/errors; format passed, privacy1105,
  Markdown146/414 links zero problems; existing full-build/workspace warnings retained
- actual isolated synthetic PostgreSQL initial/concurrent/crash/restart passed exact
  decimal/preferences/taxonomy/aliases/version/time preservation, two-user isolation,
  duplicate/replayed bootstrap and alias updates, atomic service/outbox failure rollback,
  corrupt snapshots, separate runtime-role SQL denial, deletion/replay markers,
  pending-safe retention across 32 published intents with at most 25 deletes per pass
- seven HTTP/boundary regressions verify missing/null registration owners and
  oversized/control correlation metadata fail validation before storage in both
  adapters; actual PostgreSQL confirms no state/outbox mutation on invalid requests
- owned fixtures stopped; Confluence19 post-merge bodies read-back verified;
  no paid provider/API/runner/credit/fallback, production mutation or SCM registration
- native broker/readiness remains FIN-276; integrated retention/recovery FIN-286,
  other native domains and actual installer/three-Windows acceptance remain required
- active preferences/catalogs do not expire; deletion removes values but retains
  minimal encrypted replay markers. Pending routing metadata has no automatic TTL
  until qualified transport/reconciliation; this is not full cross-service erasure
- FIN-270 remains In Progress 6/16; FIN-38 P9 is 12/36 (33.3%), first-user Not Ready

Previous canonical closure (historical Identity snapshot):

- FIN-282 - Persist native identity accounts sessions and challenge state
- implementation PR: https://github.com/Marachert/FinancialAssistant.Host/pull/276
- actual merge: https://github.com/Marachert/FinancialAssistant.Host/commit/d8530fb8a6dde87559fc07fc52dd89352e0afd9e
- merged at: `2026-10-11T12:14:21Z`
- final head: `e12c388f842942fa8951c53c0e2ca2370e0df559`
- exact-head CI: Backend38137763784, Mobile38137763793, Admin38137763841;
  all five checks succeeded, no pending/cancelled/skipped checks
- previous/current completion: 186/212 (87.7%) to 187/212 (88.2%); +0.5 pp before rounding
- both provider-HTTP-lock and hard-retention findings fixed/regressed, replied with
  commit/test/CI evidence and resolved only after green; final channels clear
- local full baseline929/929 across24projects, zero skips; reviewed Identity69/69,
  scoped builds0warnings/0errors; full format0/1033, privacy1085, Markdown145/399links0problems
- real isolated synthetic PostgreSQL initial/crash/restart passed account/provider
  uniqueness, atomic service outbox failure rollback, session rotation/reuse/owner
  isolation, phone workflow/rates, bounded retention/owned-session deletion, cursor
  owner checks, unknown DTO rejection and compatible key/schema checks
- review regressions keep two synthetic provider validators pending while a normal
  owned read completes within2seconds; reject90+30/hard90, accept120, retain session
  at document age119/delete120 and reject an incompatible shorter stored policy
- owned fixtures stopped; Confluence19 current bodies read-back verified and
  141-page/1091-link audit passed; no paid provider, production mutation or SCM install
- native broker/readiness remains FIN-276; other native domains and actual installer,
  recovery, package and three-Windows acceptance remain mandatory, not inferred
- FIN-270 remains In Progress5/16; FIN-38 P9 is11/36 (30.6%), first-user Not Ready

Previous canonical closure (historical foundation snapshot):

- FIN-274 - Build service-owned PostgreSQL durable-state foundation
- implementation PR: https://github.com/Marachert/FinancialAssistant.Host/pull/274
- actual merge: https://github.com/Marachert/FinancialAssistant.Host/commit/dfa29529776ff91f7bb86720bccd45c2198d2b48
- merged at: `2026-10-11T04:34:34Z`
- final head: `c5ff972e4afeafb76b8a517e33a1d86b662a325f`
- exact-head CI: Backend38111590088, Mobile38111590086, Admin38111590087; all five checks succeeded
- previous/current completion: 185/212 (87.3%) to 186/212 (87.7%); +0.5 pp before rounding
- two ACL/CHECK findings fixed, answered with commit/test/CI evidence and resolved
  only after green; final channels clear and protected merge verified
- local full baseline923/923 across24projects, zero skipped; final fixture-safety3/3
  and persistence19/19; build/format/privacy1069/docs144links388 pass
- actual isolated PostgreSQL18.6 role/schema/table/column ACL, default-grant/missing-role,
  all seven CHECKs, weakened/unvalidated constraints, key loss/rotation, revision,
  replay, atomic-prefix rollback, cancellation/concurrency and restart checks passed;
  owned processes stopped, no SCM registration or package qualification inferred
- domain behavior remains FIN-282/283/284/285/286 with FIN-275/276/277 prerequisites;
  FIN-270 is In Progress with 4/16 children Done and first-user testing is Not Ready

Previous canonical closure (historical pre-decomposition scope):

- FIN-273 - Host backend services natively under Windows Service Control Manager
- implementation PR: https://github.com/Marachert/FinancialAssistant.Host/pull/272
- actual merge: https://github.com/Marachert/FinancialAssistant.Host/commit/ea766a9172d0b4ad49ce49c4721b6e7c3d676f52
- merged at: `2026-10-10T18:37:39Z`
- final head: `9df3bc0588a57cbc4695aa4635b952bf0f1e10b5`
- exact-head CI: Backend 38075754127, Mobile 38075754090, Admin Web 38075754085
- previous current-scope completion: 184 / 207, or 88.9%
- current completion: 185 / 207, or 89.4%
- change: +0.5 percentage points
- at FIN-273 closure FIN-270 had 3/11 children Done; after decomposition it has
  3/16 children Done and remained In Progress; FIN-274 was next, subject to fresh
  dependency-aware selection including FIN-207/208/209
- one actionable Monitoring target review thread was fixed, answered and resolved
  after fixing-head CI;
  final merge channels were clear, with no branch-protection bypass
- local repository tests 310/310, baseline other projects 587 cases, final hosting
  54/54; zero skipped; format, privacy1056 and docs143/381 checks pass
- all fifteen win-x64 candidates verified at source67afe6a; corrected Monitoring
  published at9df3; unqualified/uninstalled, with synthetic Gateway console smoke
- first-user testing remains Not Ready; console/unit evidence is not SCM acceptance

Previous-scope FIN-203 closure and its +0.5 pp remain in history below.
The fresh 2026-09-14 census contains 269 unique issues and confirms 182/196 after
FIN-203 closure. All 13 P8 children are Done. FIN-36 remains In Progress because
its integrated runtime DoD is not verified; task counts do not close that gate.
Prior FIN-269 documentation maintenance has no parent or children
and contributes neither numerator nor denominator; its +0.0 pp closure and
publication through PRs #248/#249 remain historical evidence below.

Scope reconciliation on 2026-09-06: the fresh census contains 268 unique Jira
issues. FIN-131 is Done but currently has no parent, confirmed by a direct Jira
read. Under the calculation contract it is outside the canonical hierarchy,
reducing both the previous numerator and denominator by one. On the current
scope FIN-193 moves 171/196 (87.2%) to 172/196 (87.8%): an unrounded increase of
0.5102 percentage points, reported as +0.5 pp. Independently rounded endpoint
percentages can differ by 0.6 pp. Historical snapshots remain unchanged; no Jira
parent was silently repaired and no previously delivered work was undone.

FIN-122 is an exact later duplicate of FIN-121, FIN-125 is an exact later
duplicate of FIN-124, FIN-127 is an exact later duplicate of FIN-126, and
FIN-135 is an exact later duplicate of FIN-134, FIN-220 is an exact later
duplicate of FIN-138, and FIN-141, FIN-142, and FIN-143 are exact later
duplicates of FIN-140, FIN-146 is an exact later duplicate of FIN-145, and
FIN-148, FIN-149, FIN-150, and FIN-151 are exact later duplicates of FIN-147,
FIN-153 and FIN-154 are exact later duplicates of FIN-152, and FIN-156 and
FIN-157 are exact later duplicates of FIN-155. FIN-159, FIN-160, and FIN-161 are
exact later duplicates of FIN-158, and FIN-163 and FIN-164 are exact later
duplicates of FIN-162, and FIN-167, FIN-168, and FIN-169 are exact later
duplicates of FIN-166. FIN-171, FIN-172, and FIN-173 are exact later duplicates
of FIN-170, and FIN-175, FIN-176, and FIN-177 are exact later duplicates of
FIN-174. Closing these duplicates does not change the numerator or denominator.
FIN-179 is an exact later duplicate of FIN-178 and also does not change the
numerator or denominator.

FIN-183, FIN-187, FIN-188, and FIN-190 are Jira-ranked leaves with no parent
field. Their
implementations are not duplicates, but they are outside the canonical epic
hierarchy defined by the calculation contract, so their closures leave the
numerator and denominator unchanged.

Current delivery (native scope revision supersedes previous scope totals):

- FIN-283 is verified Done after guarded PR278 merge at
  `dba7624e84bcfac7991fc41610e8ad829716e0e6`; separate Profile/Category durable
  adapters and atomic outbox intents are not broker/installer/first-user acceptance
- FIN-282 is verified Done after guarded PR276 merge at
  `d8530fb8a6dde87559fc07fc52dd89352e0afd9e`; Identity durable adapters are not
  reliable broker delivery, WPF/SCM installation or first-user acceptance
- FIN-274 is verified Done after guarded PR274 merge at
  `dfa29529776ff91f7bb86720bccd45c2198d2b48`; per-service PostgreSQL infrastructure
  is not completion of the separately tracked native domain adapters
- FIN-273 is Done through guarded merged PR #272; FIN-271/272 remain historical
- native hosting implementation is merged at `ea766a9172d0b4ad49ce49c4721b6e7c3d676f52`
  after exact-head Backend, Mobile and Admin Web CI and a clear review-channel audit
- current canonical completion is 188/212 (88.7%) after FIN-283 Profile/Category closure;
  five persistence leaves retain the original domain requirements. FIN-271/272/273
  remain Done and earlier closure percentages
  remain historical, with no lost delivery
- FIN-31 P7 progress is 18/18 (100.0%); all 33 direct children and the epic are
  verified Done
- FIN-36 P8 progress is 13/13 (100.0%); all children are Done and the epic remains
  In Progress for unverified integrated log/metric/trace, audit, admin and MCP evidence
- FIN-38 P9 progress is 12/36 (33.3%); the epic remains In Progress
- FIN-27 remains In Progress because its event-driven notification delivery
  Definition of Done is not yet satisfied
- first-user testing remains Not Ready because runtime P6, P8, and P9 gates
  remain open
- no paid provider, exporter, alert destination, or additional credits were used
- FIN-200 defines seven operational readiness areas and a separate required rollback
  gate, with a blank Blocked decision record and explicit reviewed-head, actual-merge,
  deployed-candidate and CI provenance. It is a manual template, not a Go validator
- FIN-199's 20 planned scenarios remain execution requirements; source test references
  do not establish that every planned case or host acceptance passed
- no evaluator, paging destination, provider send, administrator provisioning or
  runtime acceptance is enabled by this checklist definition
- FIN-201 coordinates five QA lanes, twelve critical smoke flows, area owners,
  regression cadence and candidate/CI/artifact provenance across the full POC;
  only explicit Pass with valid evidence qualifies, never blank/Fail/Blocked/Not run
- FIN-202 defines seven full user journeys with prerequisites, happy/negative paths,
  explicit backend/mobile/runtime dependencies and a checked synthetic arithmetic
  fixture. Confirmation intent, owner commit, convergence and notification receipt
  remain distinct; no public/native/provider execution is claimed
- FIN-203 maps seven production-like QA areas to owners, execution lanes,
  candidate evidence and blockers within the shared strategy; static checks,
  test hosts and plan completion do not grant runtime or release acceptance
- historical FIN-271 validation: local 180/180 repository tests, focused format, 1036-file privacy scan and
  140 Markdown files/334 local links passed; all five exact-head CI checks were green
- one valid infrastructure-index finding was fixed with regression coverage,
  answered and resolved after green CI; no blocking review/comment remained
- forty Confluence native decision bodies and the new canonical installer page
  were read-back verified; original content/history retained
- all 139 Confluence pages were audited; 118 updated and 79 empty content pages filled;
  historical evidence and three intentional templates were preserved
- FIN-269 GitHub documentation synchronization is merged through PR #248 and Done;
  all five exact-head CI checks passed and every review channel was empty
- current documentation includes the implementation map, preferred storage policy,
  source-to-Confluence maintenance map and explicit financial authorization limits
- FIN-270 is the priority parent with 6/16 children Done; FIN-271/272/273/274/282/283
  concept, inventory, hosting, foundation, Identity and Profile/Category are Done,
  without installed acceptance
- next ranked native implementation leaf is FIN-284 after FIN-283 closure
  publication; re-read its hierarchy and dependencies before branching
- FIN-274 is the PostgreSQL foundation; FIN-282 Identity, FIN-283 Profile/Category,
  FIN-284 Intake, FIN-285 Receipt/OCR/AI metadata and FIN-286 persistence integration
  retain its original domain/verification scope; prerequisites can override rank
- FIN-207/208/209 TLS, protected secrets and backup/recovery remain separate
  required release gates; satisfy native runtime dependencies before FIN-204 E2E

## Epic Progress

| Epic | POC area | Done | Total | Progress |
| --- | --- | ---: | ---: | ---: |
| FIN-1 | P0 Product clarification and release scope | 3 | 3 | 100.0% |
| FIN-5 | P1 Architecture definition and technical governance | 17 | 17 | 100.0% |
| FIN-10 | P2 Repository, DevOps, and local platform foundation | 36 | 36 | 100.0% |
| FIN-14 | P3 API Gateway, authentication, and security foundation | 28 | 28 | 100.0% |
| FIN-18 | P4 Financial core backend services | 22 | 22 | 100.0% |
| FIN-23 | P5 AI orchestration and OCR automation | 19 | 19 | 100.0% |
| FIN-27 | P6 Analytics, score, recommendations, and notifications | 20 | 20 | 100.0% |
| FIN-31 | P7 Mobile app UX and React Native implementation | 18 | 18 | 100.0% |
| FIN-36 | P8 Observability, admin UI, audit, and MCP tooling | 13 | 13 | 100.0% |
| FIN-38 | P9 Testing, Windows deployment, and release readiness | 12 | 36 | 33.3% |
| **Total** | **Canonical POC leaf scope** | **188** | **212** | **88.7%** |

## First-User-Test Gates

The POC can be handed to first test users only when all of these gates are
satisfied:

- a usable client flow exists for authentication, free-form transaction input,
  draft review and confirmation, receipt upload, dashboard, and settings;
- the authoritative financial core supports the required income, expense,
  balance, category, and limit flows;
- AI and OCR provider paths have safe privacy, cost, fallback, and release
  controls;
- analytics and user-facing insight flows required by the PoC scope are usable;
- monitoring and support diagnostics expose safe operational state;
- integration, contract, privacy, and end-to-end tests pass with synthetic data;
- the WPF installer deploys the complete native Windows stack without Docker/WSL;
  install/reboot/update/repair/recovery/uninstall and actual first-user environment
  are verified on Windows 11 and Server 2022/2025 Desktop Experience.

Current blocking areas are runtime P6, P8, and P9. P7 now has authentication,
free-form transaction capture, receipt upload, editable draft review, backend
rejection and confirmation, onboarding/profile setup, dashboard,
score/recommendation, settings, analytics/category-chart screens, and the
notification inbox, recommendation detail and lifecycle actions, resilient
offline/error states, and shared loading/empty patterns. Its documented
release-ready smoke and regression validation covers
both Android and iOS paths. FIN-43 now supplies repository-verifiable store
configuration and controlled internal-test runbooks; external account records,
signing, builds, submissions, tester installation, and approved-host runtime
evidence remain part of P9.

## Calculation Contract

This file is updated after every Jira leaf closure. Exact duplicate closures are
recorded with a +0.0 percentage-point change.

1. Source data is the current Jira `FIN` project hierarchy.
2. POC scope is the canonical leaf-ticket scope under epics FIN-1, FIN-5,
   FIN-10, FIN-14, FIN-18, FIN-23, FIN-27, FIN-31, FIN-36, and FIN-38.
3. A leaf ticket is a non-epic issue with no canonical child issue.
4. A ticket contributes to the numerator only when Jira status is Done.
   In Progress receives no partial credit.
5. Issues with the same parent, normalized summary, and normalized description
   are exact duplicates. The lowest Jira key is canonical; later duplicates are
   excluded from both numerator and denominator.
6. The displayed percentage is `Done canonical leaves / all canonical leaves`,
   rounded to one decimal place.
7. Readiness also requires every first-user-test gate above. A high percentage
   alone does not declare the POC usable.

## Closure History

| Recorded at | Jira ticket | Result | POC readiness | Change |
| --- | --- | --- | ---: | ---: |
| 2026-07-30T12:22:16+03:00 | FIN-118 | Merged and Done | 45.9% | +0.5 pp |
| 2026-07-30T13:09:43+03:00 | FIN-121 | Closure delivered by PR 60 | 46.4% | +0.5 pp |
| 2026-07-30T13:59:47+03:00 | FIN-123 | Closure delivered by PR 61 | 46.9% | +0.5 pp |
| 2026-07-30T16:52:31+03:00 | FIN-124 | Merged and Done | 47.4% | +0.5 pp |
| 2026-07-30T17:06:50+03:00 | FIN-50 | Recovered merged work and closed Jira | 48.0% | +0.5 pp |
| 2026-07-30T17:26:23+03:00 | FIN-56 | Recovered delivered work and closed Jira | 48.5% | +0.5 pp |
| 2026-07-30T17:34:05+03:00 | FIN-54 | Recovered delivered work and closed Jira | 49.0% | +0.5 pp |
| 2026-07-30T17:41:07+03:00 | FIN-55 | Recovered delivered work and closed Jira | 49.5% | +0.5 pp |
| 2026-07-30T17:49:00+03:00 | FIN-57 | Recovered delivered CI baseline and closed Jira | 50.0% | +0.5 pp |
| 2026-07-30T18:16:21+03:00 | FIN-59 | Closure delivered by PR 69 | 50.5% | +0.5 pp |
| 2026-07-31T11:22:00+03:00 | FIN-60 | Closure delivered by PR 71 | 51.0% | +0.5 pp |
| 2026-07-31T11:38:00+03:00 | FIN-62 | Closure delivered by PR 73 | 51.5% | +0.5 pp |
| 2026-07-31T11:58:30+03:00 | FIN-64 | Closure delivered by PR 75 | 52.0% | +0.5 pp |
| 2026-07-31T12:25:54+03:00 | FIN-65 | Closure delivered by PR 77 | 52.6% | +0.6 pp |
| 2026-07-31T13:01:00+03:00 | FIN-66 | Closure delivered by PR 79 | 53.1% | +0.5 pp |
| 2026-08-01T17:04:30+03:00 | FIN-67 | Closure delivered by PR 81 | 53.6% | +0.5 pp |
| 2026-08-01T17:18:30+03:00 | FIN-68 | Closure delivered by PR 83 | 54.1% | +0.5 pp |
| 2026-08-01T17:34:05+03:00 | FIN-69 | Closure delivered by PR 85; FIN-10 completed | 54.6% | +0.5 pp |
| 2026-08-01T17:42:03+03:00 | FIN-87 | Recovered completed FIN-19 delivery from PRs 40 and 42 | 55.1% | +0.5 pp |
| 2026-08-01T17:57:00+03:00 | FIN-88 | Closure delivered by PR 88 | 55.6% | +0.5 pp |
| 2026-08-01T18:05:00+03:00 | FIN-89 | Recovered completed FIN-20 delivery from PR 43 | 56.1% | +0.5 pp |
| 2026-08-01T18:12:00+03:00 | FIN-90 | Closure delivered by PR 91 | 56.6% | +0.5 pp |
| 2026-08-01T19:12:00+03:00 | FIN-91 | Closure delivered by PR 93 | 57.1% | +0.5 pp |
| 2026-08-01T19:43:00+03:00 | FIN-92 | Closure delivered by PR 95 | 57.7% | +0.6 pp |
| 2026-08-01T20:34:00+03:00 | FIN-93 | Closure delivered by PR 97 | 58.2% | +0.5 pp |
| 2026-08-02T10:30:00+03:00 | FIN-94 | Closure delivered by PR 99 | 58.7% | +0.5 pp |
| 2026-08-02T11:38:00+03:00 | FIN-95 | Closure delivered by PR 101 | 59.2% | +0.5 pp |
| 2026-08-02T11:55:00+03:00 | FIN-96 | Closure delivered by PR 103 | 59.7% | +0.5 pp |
| 2026-08-02T12:14:00+03:00 | FIN-97 | Closure delivered by PR 105 | 60.2% | +0.5 pp |
| 2026-08-02T12:26:00+03:00 | FIN-98 | Closure delivered by PR 107 | 60.7% | +0.5 pp |
| 2026-08-02T12:43:00+03:00 | FIN-99 | Closure delivered by PR 109 | 61.2% | +0.5 pp |
| 2026-08-02T12:55:30+03:00 | FIN-100 | Closure delivered by PR 111 | 61.7% | +0.5 pp |
| 2026-08-02T13:04:30+03:00 | FIN-101 | Closure delivered by PR 113 | 62.2% | +0.5 pp |
| 2026-08-02T13:20:30+03:00 | FIN-102 | Closure delivered by PR 115 | 62.8% | +0.6 pp |
| 2026-08-02T13:33:30+03:00 | FIN-103 | Closure delivered by PR 117 | 63.3% | +0.5 pp |
| 2026-08-02T14:24:20+03:00 | FIN-104 | Closure delivered by PR 119; FIN-18 completed | 63.8% | +0.5 pp |
| 2026-08-02T16:35:09.228+03:00 | FIN-28 | Closure delivered by PR 121 | 64.3% | +0.5 pp |
| 2026-08-02T17:16:06+03:00 | FIN-29 | Closure delivered by PR 123 | 64.8% | +0.5 pp |
| 2026-08-02T20:28:45+03:00 | FIN-30 | Closure delivered by PR 125 | 65.3% | +0.5 pp |
| 2026-08-02T20:39:30+03:00 | FIN-126 | Recovered analytics baseline from merged PR 121 | 65.8% | +0.5 pp |
| 2026-08-02T20:45:10+03:00 | FIN-127 | Exact duplicate of FIN-126; Jira-linked and Done | 65.8% | +0.0 pp |
| 2026-08-02T21:00:20+03:00 | FIN-128 | Closure delivered by PR 129 | 66.3% | +0.5 pp |
| 2026-08-02T21:23:30+03:00 | FIN-129 | Closure delivered by PR 131 | 66.8% | +0.5 pp |
| 2026-08-02T21:29:00+03:00 | FIN-130 | Recovered Financial Score baseline from merged PR 123 | 67.3% | +0.5 pp |
| 2026-08-09T09:34:00+03:00 | FIN-131 | Closure delivered by PR 134 | 67.9% | +0.6 pp |
| 2026-08-09T09:47:30+03:00 | FIN-132 | Closure delivered by PR 136 | 68.4% | +0.5 pp |
| 2026-08-09T10:12:00+03:00 | FIN-133 | Closure delivered by PR 138 | 68.9% | +0.5 pp |
| 2026-08-09T10:32:00+03:00 | FIN-134 | Closure delivered by PR 140 | 69.4% | +0.5 pp |
| 2026-08-09T10:39:00+03:00 | FIN-135 | Exact duplicate of FIN-134; Jira-linked and Done | 69.4% | +0.0 pp |
| 2026-08-09T10:51:30+03:00 | FIN-136 | Closure delivered by PR 143 | 69.9% | +0.5 pp |
| 2026-08-09T11:06:00+03:00 | FIN-137 | Closure delivered by PR 145 | 70.4% | +0.5 pp |
| 2026-08-09T12:14:07+03:00 | FIN-138 | Closure delivered by PR 147 | 70.9% | +0.5 pp |
| 2026-08-09T12:31:43+03:00 | FIN-139 | Closure delivered by PR 149 | 71.4% | +0.5 pp |
| 2026-08-09T12:56:17+03:00 | FIN-140 | Closure delivered by PR 151 | 71.9% | +0.5 pp |
| 2026-08-09T14:04:00+03:00 | FIN-144 | Closure delivered by PR 153 | 72.4% | +0.5 pp |
| 2026-08-09T14:24:10+03:00 | FIN-145 | Closure delivered by PR 155 | 73.0% | +0.6 pp |
| 2026-08-09T14:38:12+03:00 | FIN-147 | Closure delivered by PR 157 | 73.5% | +0.5 pp |
| 2026-08-09T14:55:00+03:00 | FIN-152 | Closure delivered by PR 159 | 74.0% | +0.5 pp |
| 2026-08-09T15:20:30+03:00 | FIN-155 | Closure delivered by PR 161 | 74.1% | +0.1 pp |
| 2026-08-09T15:26:30+03:00 | FIN-141 | Exact duplicate of FIN-140; Jira-linked and Done | 74.1% | +0.0 pp |
| 2026-08-09T15:33:30+03:00 | FIN-142 | Exact duplicate of FIN-140; Jira-linked and Done | 74.1% | +0.0 pp |
| 2026-08-09T15:39:30+03:00 | FIN-143 | Exact duplicate of FIN-140; Jira-linked and Done | 74.1% | +0.0 pp |
| 2026-08-09T15:45:30+03:00 | FIN-146 | Exact duplicate of FIN-145; Jira-linked and Done | 74.1% | +0.0 pp |
| 2026-08-09T16:17:30+03:00 | FIN-148 | Exact duplicate of FIN-147; Jira-linked and Done | 74.1% | +0.0 pp |
| 2026-08-09T16:22:00+03:00 | FIN-149 | Exact duplicate of FIN-147; Jira-linked and Done | 74.1% | +0.0 pp |
| 2026-08-09T16:26:30+03:00 | FIN-150 | Exact duplicate of FIN-147; Jira-linked and Done | 74.1% | +0.0 pp |
| 2026-08-09T16:30:00+03:00 | FIN-151 | Exact duplicate of FIN-147; Jira-linked and Done | 74.1% | +0.0 pp |
| 2026-08-16T10:06:00+03:00 | FIN-153 | Exact duplicate of FIN-152; Jira-linked and Done | 74.1% | +0.0 pp |
| 2026-08-17T18:57:00+03:00 | FIN-154 | Exact duplicate of FIN-152; Jira-linked and Done | 74.1% | +0.0 pp |
| 2026-08-17T19:25:30+03:00 | FIN-156 | Exact duplicate of FIN-155; Jira-linked and Done | 74.1% | +0.0 pp |
| 2026-08-17T19:31:00+03:00 | FIN-157 | Exact duplicate of FIN-155; Jira-linked and Done | 74.1% | +0.0 pp |
| 2026-08-17T19:37:00+03:00 | FIN-220 | Exact duplicate of FIN-138; Jira-linked and Done | 74.1% | +0.0 pp |
| 2026-08-17T19:58:15+03:00 | FIN-32 | Closure delivered by PR 176 | 74.6% | +0.5 pp |
| 2026-08-20T19:03:00+03:00 | FIN-33 | Closure delivered by PR 178 | 75.1% | +0.5 pp |
| 2026-08-21T19:58:00+03:00 | FIN-34 | Closure delivered by PR 180 | 75.6% | +0.5 pp |
| 2026-08-22T19:10:00+03:00 | FIN-35 | Closure delivered by PR 182 | 76.1% | +0.5 pp |
| 2026-08-22T19:27:00+03:00 | FIN-158 | Recovered mobile UX flow baseline from merged PR 176 | 76.6% | +0.5 pp |
| 2026-08-22T19:35:00+03:00 | FIN-159 | Exact duplicate of FIN-158; Jira-linked and Done | 76.6% | +0.0 pp |
| 2026-08-22T19:42:00+03:00 | FIN-160 | Exact duplicate of FIN-158; Jira-linked and Done | 76.6% | +0.0 pp |
| 2026-08-22T19:49:00+03:00 | FIN-161 | Exact duplicate of FIN-158; Jira-linked and Done | 76.6% | +0.0 pp |
| 2026-08-22T19:55:00+03:00 | FIN-162 | Recovered mobile design-system baseline from merged PR 176 | 77.2% | +0.6 pp |
| 2026-08-22T20:00:00+03:00 | FIN-163 | Exact duplicate of FIN-162; Jira-linked and Done | 77.2% | +0.0 pp |
| 2026-08-22T20:06:00+03:00 | FIN-164 | Exact duplicate of FIN-162; Jira-linked and Done | 77.2% | +0.0 pp |
| 2026-08-22T20:12:00+03:00 | FIN-165 | Recovered React Native app foundation from merged PR 178 | 77.7% | +0.5 pp |
| 2026-08-22T20:18:00+03:00 | FIN-166 | Recovered mobile authentication screens from merged PR 178 | 78.2% | +0.5 pp |
| 2026-08-22T20:24:00+03:00 | FIN-167 | Exact duplicate of FIN-166; Jira-linked and Done | 78.2% | +0.0 pp |
| 2026-08-22T20:30:00+03:00 | FIN-168 | Exact duplicate of FIN-166; Jira-linked and Done | 78.2% | +0.0 pp |
| 2026-08-22T20:36:00+03:00 | FIN-169 | Exact duplicate of FIN-166; Jira-linked and Done | 78.2% | +0.0 pp |
| 2026-08-23T10:23:00+03:00 | FIN-170 | Closure delivered by PR 196 | 78.7% | +0.5 pp |
| 2026-08-23T10:31:00+03:00 | FIN-171 | Exact duplicate of FIN-170; Jira-linked and Done | 78.7% | +0.0 pp |
| 2026-08-23T10:37:00+03:00 | FIN-172 | Exact duplicate of FIN-170; Jira-linked and Done | 78.7% | +0.0 pp |
| 2026-08-23T10:43:00+03:00 | FIN-173 | Exact duplicate of FIN-170; Jira-linked and Done | 78.7% | +0.0 pp |
| 2026-08-23T11:01:00+03:00 | FIN-174 | Closure delivered by PR 201 | 79.2% | +0.5 pp |
| 2026-08-23T11:09:00+03:00 | FIN-175 | Exact duplicate of FIN-174; Jira-linked and Done | 79.2% | +0.0 pp |
| 2026-08-23T11:18:00+03:00 | FIN-176 | Exact duplicate of FIN-174; Jira-linked and Done | 79.2% | +0.0 pp |
| 2026-08-23T11:25:00+03:00 | FIN-177 | Exact duplicate of FIN-174; Jira-linked and Done | 79.2% | +0.0 pp |
| 2026-08-23T11:56:00+03:00 | FIN-178 | Recovered free-form capture from merged PR 180 | 79.7% | +0.5 pp |
| 2026-08-23T12:05:00+03:00 | FIN-179 | Exact duplicate of FIN-178; Jira-linked and Done | 79.7% | +0.0 pp |
| 2026-08-23T12:13:00+03:00 | FIN-180 | Recovered receipt upload from merged PR 180 | 80.2% | +0.5 pp |
| 2026-08-23T13:08:00+03:00 | FIN-181 | Closure delivered by PR 210 after P1 gateway-route fix | 80.7% | +0.5 pp |
| 2026-08-23T13:37:00+03:00 | FIN-182 | Closure delivered by PR 212 | 81.2% | +0.5 pp |
| 2026-08-23T14:54:00+03:00 | FIN-184 | Closure delivered by PR 214 | 81.7% | +0.5 pp |
| 2026-08-23T15:05:00+03:00 | FIN-185 | Recovered API and state foundation from PRs 178, 180, and 182 | 82.2% | +0.5 pp |
| 2026-08-23T15:37:00+03:00 | FIN-186 | Closure delivered by PR 217 | 82.7% | +0.5 pp |
| 2026-08-24T16:21:00+03:00 | FIN-189 | Closure delivered by PR 219; FIN-31 completed | 83.2% | +0.5 pp |
| 2026-08-24T17:11:00+03:00 | FIN-37 | Closure delivered by PR 221 | 83.8% | +0.6 pp |
| 2026-08-24T17:51:00+03:00 | FIN-39 | Closure delivered by PR 223 | 84.3% | +0.5 pp |
| 2026-08-24T18:50:11+03:00 | FIN-40 | Closure delivered by PR 225 | 84.8% | +0.5 pp |
| 2026-08-24T19:22:06+03:00 | FIN-41 | Closure delivered by PR 227 | 85.3% | +0.5 pp |
| 2026-08-24T20:03:11+03:00 | FIN-42 | Closure delivered by PR 229 | 85.8% | +0.5 pp |
| 2026-08-28T18:40:13+03:00 | FIN-43 | Closure delivered by PR 231 | 86.3% | +0.5 pp |
| 2026-08-28T19:23:39+03:00 | FIN-183 | Closure delivered by PR 233; outside canonical epic hierarchy | 86.3% | +0.0 pp |
| 2026-08-29T18:33:09+03:00 | FIN-187 | Closure delivered by PR 235; outside canonical epic hierarchy | 86.3% | +0.0 pp |
| 2026-08-29T20:10:12+03:00 | FIN-188 | Closure delivered by PR 237; outside canonical epic hierarchy | 86.3% | +0.0 pp |
| 2026-08-30T10:19:39+03:00 | FIN-190 | Closure delivered by PR 239; outside canonical epic hierarchy | 86.3% | +0.0 pp |
| 2026-09-04T18:13:11+03:00 | FIN-191 | Closure delivered by PR 241 | 86.8% | +0.5 pp |
| 2026-09-05T17:27:06+03:00 | FIN-192 | Closure delivered by PR 243 | 87.3% | +0.5 pp |
| 2026-09-06T09:50:00+03:00 | FIN-193 | Closure delivered by PR 245; FIN-131 orphan scope reconciled above | 87.8% | +0.5 pp |
| 2026-09-10T20:11:22+03:00 | FIN-194 | PR 247 verified merged; Jira Done verified afterward; 173/196 canonical leaves | 88.3% | +0.5 pp |
| 2026-09-12 | FIN-269 | PR 248 verified merged; Jira Done verified; documentation maintenance outside canonical feature scope | 88.3% | +0.0 pp |
| 2026-09-12 | FIN-195 | PR 250 verified merged and Jira Done; operational index/query design, not runtime deployment | 88.8% | +0.5 pp |
| 2026-09-13 | FIN-196 | PR 252 verified merged and Jira Done; read-only MCP diagnostics definition, not runtime enablement | 89.3% | +0.5 pp |
| 2026-09-13 | FIN-197 | PR 254 verified merged and Jira Done; failed-job support workflow, not runtime recovery or provider enablement | 89.8% | +0.5 pp |
| 2026-09-13 | FIN-198 | PR 256 verified merged and Jira Done after two review fixes; alert definitions and response targets, not runtime paging | 90.3% | +0.5 pp |
| 2026-09-14 | FIN-199 | PR 258 verified merged and Jira Done; observability/admin test plan, not execution of all planned scenarios or host acceptance | 90.8% | +0.5 pp |
| 2026-09-14 | FIN-200 | PR 260 verified merged and Jira Done after provenance and rollback review fixes; readiness checklist definition, not runtime Go approval; FIN-36 remains In Progress for integrated evidence | 91.3% | +0.5 pp |
| 2026-09-14 | FIN-201 | PR 262 verified merged and Jira Done after explicit-Pass review fix; full POC QA strategy, not host/device/provider/store acceptance; FIN-38 remains In Progress | 91.8% | +0.5 pp |
| 2026-09-14 | FIN-202 | PR 264 verified merged and Jira Done; seven MVP E2E scenario definitions and synthetic fixture, not actual public/native/provider acceptance; FIN-38 remains In Progress | 92.3% | +0.5 pp |
| 2026-09-14 | FIN-203 | PR 266 verified merged and Jira Done; seven-area production-like QA scope crosswalk, not runtime acceptance or release approval; FIN-38 remains In Progress | 92.9% | +0.5 pp |
| 2026-09-15 | FIN-271 | PR 268 verified merged and Jira Done after infrastructure-index review fix; Windows-native WPF installer concept/backlog only; scope expanded to 207 before closure, not a completed installer | 88.4% | +0.5 pp |
| 2026-10-10 | FIN-272 | PR 270 verified merged as 52ca12d5269f8ebe6bf20c958b553f540cbcc151 and Jira Done; 184/207 after live census; inventory/inspection only under owner-approved phase split, not package qualification or installed acceptance | 88.9% | +0.5 pp |
| 2026-10-10 | FIN-273 | PR 272 verified merged as ea766a9172d0b4ad49ce49c4721b6e7c3d676f52 and Jira Done; 185/207 after live census; hosting code/free verification under owner-approved phase split, not SCM installation or first-user acceptance | 89.4% | +0.5 pp |
| 2026-10-10 | FIN-274 decomposition | Original persistence scope retained in foundation plus FIN-282–286; five additional leaf units, no completed work lost/new product features; denominator 207 to 212, Done remains 185 | 87.3% | -2.1 pp scope adjustment |
| 2026-10-11 | FIN-274 | PR274 verified merged as dfa29529776ff91f7bb86720bccd45c2198d2b48 and Jira Done; 186/212 after fresh census; service-owned PostgreSQL foundation only, domain adapters and installed acceptance remain mandatory | 87.7% | +0.5 pp |
| 2026-10-11 | FIN-282 | PR276 verified merged as d8530fb8a6dde87559fc07fc52dd89352e0afd9e and Jira Done; fresh census187/212; native Identity state/outbox with provider-lock and hard-retention review regressions, not broker/installer/installed acceptance | 88.2% | +0.5 pp |
| 2026-10-11 | FIN-283 | PR278 verified merged as dba7624e84bcfac7991fc41610e8ad829716e0e6 and Jira Done; fresh census 188/212; separate Profile/Category state and atomic outbox, reviewed input-validation regressions; not broker/installer/installed acceptance | 88.7% | +0.5 pp |
