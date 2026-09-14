# POC QA Strategy And Test Scope

Related Jira: FIN-201, parent FIN-38. This is a **plan, not execution evidence**
or release approval. It coordinates existing area plans without replacing their
stricter gates. First-user testing remains **Not Ready** until candidate-specific
evidence satisfies all required product, security, operational and release gates.
Task completion percentage measures delivered scope, not runtime readiness.

The [MVP end-to-end scenarios](mvp-e2e-scenarios.md) expand the seven complete
user journeys, negative variants, dependency gates and synthetic financial oracle.

## Scope And Ownership

The target is a simple intelligent assistant: capture income/expenses, review
text/receipt suggestions, confirm once, and understand backend-calculated facts.
Backend deterministic logic is the financial oracle. AI/OCR output is
probabilistic input, never an expected balance, score or confirmed entity.

| Area | Accountable role | Detailed execution source |
| --- | --- | --- |
| Backend identity, financial core and API/events | Backend and service owners | [Release suite](backend-release-test-suite.md), [financial validation](financial-core-validation-test-plan.md) |
| Mobile iOS/Android and public client journeys | Mobile owner and QA | [Mobile smoke/regression](mobile-smoke-regression-test-plan.md) |
| AI/OCR and draft safety | AI/OCR and Intake owners | [Integration plan](ai-ocr-integration-test-plan.md), [provider readiness](ai-ocr-release-readiness-checklist.md) |
| Analytics, score, recommendations and notifications | Insights and notification owners | [Insights validation](insights-validation-test-plan.md), [P6 readiness](insights-release-readiness-checklist.md) |
| Admin web, Monitoring, Audit, support and MCP | Operations, admin web and security owners | [Observability/admin plan](observability-admin-test-plan.md), [P8 readiness](operational-release-readiness.md) |
| Storage, deployment, TLS and recovery | Platform owner | [Windows runbook](../../infra/windows-poc/README.md) |
| Privacy and controlled store distribution | Security, mobile, account and release owners | [Store tracks](../delivery/mobile-store-release-tracks.md), [privacy policy](../legal/privacy-policy.md) |

QA coordinates the matrix and defect register; each area owner executes and
triages its rows. The release owner names a primary and backup for every area in
the restricted candidate record before execution. Unassigned required work is
Blocked. Role names here do not invent assigned people or approvals. Product and
security owners approve declared capabilities; only the release owner records
the final dated decision after all independent gates pass.

FIN-202 expands journey scenarios; FIN-203 is a separately ranked QA ticket,
not automatically closed as a duplicate. FIN-204 owns backend E2E expansion;
FIN-205 storage validation; FIN-206/207/208/209 deployment, HTTPS, secrets and
restore; FIN-210/211 native distribution; FIN-212/213 privacy/store metadata;
FIN-215 security review; FIN-216 runtime operations; FIN-217 rollout; FIN-218
final Go/No-Go. This definition does not implement or certify those tasks.

## Test Levels And Environments

| Lane | Environment and scope | Entry / exit evidence |
| --- | --- | --- |
| L1 Deterministic | Local or CI unit/schema/contract tests with generated fixtures and fake providers | Owned source and expected calculations; all applicable assertions pass |
| L2 Service integration | ASP.NET test hosts and in-memory adapters, API/auth/event/privacy checks | Supported contracts and isolated state; TRX results tied to exact head; no claim of durable runtime integration |
| L3 Connected system | Explicitly approved isolated host with actual gateway, service-owned stores, broker and configured adapters | Approved inventory, credentials, cost and rollback; real REST/event/restart/restore evidence for candidate artifact |
| L4 Client acceptance | Approved gateway, actual admin browser and both native platforms | Build/OS/device matrix; smoke, regression, accessibility, session and permission evidence, not Node/TypeScript results alone |
| L5 Release rehearsal | Approved deployment and controlled store/tester environments | Verified TLS, backup/restore, retention, incident drill, signed artifacts, disclosures, support and Go/No-Go |

Ordinary PR verification uses L1/L2 and static client checks. L3-L5 are separate
blocked/not-run evidence until explicitly executed; no automatic fallback from a
missing host to a mock pass. POC in-memory adapters and explicit event forwarding
in the release suite do not prove broker delivery or durable database behavior.
PostgreSQL is the preferred product target; validate each candidate's actually
configured service-owned adapter, including current Elasticsearch contracts,
rather than claiming a target technology is already deployed.

Use only synthetic accounts, invented amounts/text and generated receipt images.
Reset isolated fixtures through supported APIs or recreate test accounts. Never
repair results by editing authoritative tables/projections. Keep raw fixture
payloads, tokens, prompts and receipt bytes out of logs and evidence; pseudonymous
owner hashes are restricted data, not anonymous. Record safe IDs/results only.

Default additional spend is zero. No live provider calls, paid sends/exporters,
cloud builds, account enrollment, credits, deployment or destructive recovery are
authorized here. Approval must specify owner, environment, data handling and a
maximum spend before any such lane runs. Missing approval means Blocked, not
silent omission. Offline failure simulation never switches to a paid fallback.

## Critical Smoke Flows

Run the following on each candidate after approved deployment and before tester
access. These are planned expectations, not recorded passes. Follow the linked
area plans for full setup, fixture IDs and platform-specific procedures. Use the
same candidate across the gateway/backend and client build mapping.

| ID | Owner / action | Expected result and blocking negative |
| --- | --- | --- |
| QA-SMK-001 | Identity/mobile: register, onboard, sign in, refresh, restart, sign out | Correct locale/currency/time zone and session; expired/revoked sessions and cross-owner access fail closed |
| QA-SMK-002 | Core/mobile: create synthetic income and expense, read owner records | Backend amount/currency/period policy holds; invalid amounts and foreign-owner IDs cannot mutate records |
| QA-SMK-003 | Intake/AI: submit free-form input, edit draft, reject or confirm | Suggestion remains non-authoritative until validation/confirmation; malformed, ambiguous and low-confidence input remains reviewable |
| QA-SMK-004 | OCR/mobile: upload generated image, process, review draft | Owner-scoped bounded processing; unsupported image, denied picker, timeout and unavailable provider offer truthful recovery without fabricated extraction |
| QA-SMK-005 | Intake/core: retry confirmation and lose/recover response | Exactly one authoritative record; duplicate, stale revision and concurrent confirmation cannot double count |
| QA-SMK-006 | Analytics: project creation/update/deletion and replay events | Dashboard/report/category/limit facts match deterministic policy; stale/missing projection is visible; duplicate/out-of-order events do not corrupt totals |
| QA-SMK-007 | Score/recommendations: read factors, reasons and empty/partial views | Backend score and fact-grounded reasons preserved; unavailable wording provider never invents financial advice or facts |
| QA-SMK-008 | Notifications/mobile: opt in/out, inbox/read and approved delivery | Preferences, permissions, dedupe and safe content hold; non-sending placeholder is not delivery evidence; denied permission and exhausted retry budget stay truthful |
| QA-SMK-009 | Mobile/admin web: interrupt network, switch views/account, relaunch | Loading/empty/error/offline states recover; late responses cannot leak old-session data; critical controls remain accessible on both native platforms and actual browser |
| QA-SMK-010 | Operations/security: health, logs, Audit, admin, support and MCP | All seven OPS-READY checks and rollback gate pass; missing observations, forged trust, unaudited tools or unstaffed alerts block |
| QA-SMK-011 | Platform: restart, inspect TLS and restore synthetic backup | Actual adapter/broker state reconciles without loss/duplicate sends; verified restore, retention and recovery evidence required |
| QA-SMK-012 | Release/mobile/security: install candidate from approved test track | Exact signed build, reachable policy/support, reconciled disclosures and controlled tester/support access; missing platform/account evidence blocks distribution |

Run every mobile smoke row on Android and iOS, plus required physical-device
passes before distribution. Admin web is an internal tool, not proof of a public
web application. Declare each offered client surface explicitly; an unimplemented
public web surface must not be advertised or assumed covered by admin tests.
Optional exclusions need reviewed product/security scope approval and truthful
UI/fallbacks. They cannot waive core correctness/privacy or required area gates.

## Regression Selection And Cadence

- Every PR: applicable deterministic/service tests plus all required CI checks
  on the final reviewed head, including backend build/test, format, privacy,
  mobile verification and admin web verification. A docs-only change still runs
  required CI; local subsets must be stated honestly.
- Every changed contract or financial rule: producer/consumer compatibility,
  auth/owner isolation, precision/rounding/currency, daily/weekly/monthly boundary,
  duplicate/out-of-order events, update/delete reconciliation and previous bug
  regressions. The backend policy, not a generated model answer, supplies expected
  results. Test affected neighbors as well as the changed service.
- Every provider/configuration change: disabled, timeout, cancellation, malformed
  response, ambiguous extraction, rate-limit, retry budget, privacy and approved
  sandbox mapping. Existing fake-provider tests cannot approve a new live adapter.
- Every client/session change: all relevant MOB-REG cases on each required
  platform, permission/secure-storage/backgrounding and real-browser races.
- Every release candidate: full relevant area regression, all smoke flows,
  approved-host recovery/security/operations rehearsal and store checks. Artifact,
  dependencies, configuration, trust, provider or scope changes invalidate affected
  evidence; rerun affected rows and re-evaluate the whole release decision.

## Owner Execution Sequence

1. Freeze candidate scope and assign owners/backups. Record source commit,
   reviewed PR head, independently verified actual merge, artifact digest, build
   versions, configuration reference, environment and OS/device/browser matrix.
   Verify deployed artifacts map to source. If the candidate differs from the
   reviewed head, require candidate build/test evidence and the verified mapping.
2. Verify prerequisites and privacy/cost authorization. Prepare isolated fixtures
   and approved retention/disposal, supported runtimes and known rollback path.
   Missing dependencies block that lane; do not install or provision implicitly.
3. Execute the existing [CI commands](ci.md) and [backend release suite](backend-release-test-suite.md).
   Run `npm run verify` from `mobile/app-react-native` and `web-admin/monitoring-ui`
   with approved existing dependencies. Capture exact-head CI separately from
   local results; do not promote cached/offline checks into an online audit claim.
4. Execute L3 connected flows, then L4 mobile/browser plans, then L5 operational,
   storage, security and distribution rehearsal using their owned runbooks. Do
   not start host/store commands without the prerequisites in step 2.
5. For each row record case ID, owner, UTC time, candidate/artifact/environment,
   level/platform, expected/actual safe result, Pass/Fail/Blocked/Not run,
   restricted evidence reference/hash and linked defect. Capture CI name/run URL,
   attempt, head, conclusion and completion time. A blank result never counts as
   Pass. Only an explicit Pass with valid evidence qualifies; Fail, Blocked and
   Not run never qualify.
6. Triage defects below. Fix and rerun the failed case plus affected neighboring
   regressions on the new candidate. Never weaken tests, erase blockers or retry
   until a flaky result becomes a pass; reproduce and explain nondeterminism.
7. QA checks completeness; area and security owners attest their evidence;
   release owner signs dated Go/No-Go and rollout/rollback ownership. Publish safe
   summaries in Jira/Confluence; keep actual credentials and detailed restricted
   artifacts outside public repository evidence. Recheck health at decision time.

## Release Blockers And Acceptance

QA defect severities are not the P1-P4 operational incident response classes:

| Severity | Release rule |
| --- | --- |
| P0 | Stop all distribution: privacy/secret/cross-owner exposure, data loss, corrupted authoritative facts, duplicate confirmation or unusable authentication |
| P1 | Blocks first-user testing: broken required journey, misleading success/health, unsafe recovery, inaccessible critical action or unverified required runtime behavior |
| P2 | Deferrable only for a non-critical defect with no correctness/privacy/accessibility/required-workflow impact; linked Jira, owner, workaround and dated release-owner approval required |

Any required failing, missing, stale, pending, cancelled or unexpectedly skipped
check is No-Go regardless of defect label. Required smoke/area gates cannot be
waived by calling a failure P2 or Not applicable. Keep all deferred defects visible.
The [operational record](../delivery/operational-readiness-record.json) is a P8
manual template, not the full POC approval or an automatic Go validator.

- [ ] Exact candidate/artifact/configuration/scope and all owner assignments exist.
- [ ] Required final-head CI, review channels, privacy checks and source-to-build mapping pass.
- [ ] All required smoke and area regression rows pass at their stated level/platform.
- [ ] AI/OCR readiness and P6 notifications have actual approved runtime evidence, not placeholders.
- [ ] P7 device journeys and P8 seven-area checks plus rollback gate pass independently.
- [ ] P9 storage/TLS/secrets/restore/security/operations and relevant store gates pass.
- [ ] No P0/P1, unresolved required check or unexplained flaky result remains; approved P2 records are current.
- [ ] Privacy/support endpoints, disclosures, tester cohort, response/rollback owners and dated Go/No-Go are verified.

Until all items are evidenced, the decision is **No-Go**. Neither FIN-201 Done,
green synthetic CI, all P8 leaf definitions Done nor the
[POC progress percentage](../agent/POC_PROGRESS.md) grants tester access. This plan
does not supply missing runtime results or an estimated release date.
