# MVP End-To-End User Scenarios

Related Jira: FIN-202, parent FIN-38. These are **planned scenarios, not execution
results**. The [scenario manifest](mvp-e2e-scenarios.json) contains seven journey
IDs and a synthetic arithmetic fixture; it neither runs the application nor
grants release approval. Follow the [POC QA strategy](poc-qa-strategy.md) and the
stricter [mobile matrix](mobile-smoke-regression-test-plan.md).

## Execution Boundary And Dependencies

Run the seven happy paths in order for synthetic owner A. Owner B is a separate
empty account for isolation negatives. Recreate isolated accounts or use approved
fixture setup before each independent negative variant; do not contaminate the
happy-path totals. Never seed private service tables or write read models to make
an E2E test pass. Use supported public APIs and actual owning event paths.

| Dependency | Required evidence / current limitation |
| --- | --- |
| Identity, Profile, Category and gateway | Valid bearer sessions, owner context, profile event consumption and category taxonomy; [route inventory](gateway-public-api-groups.md) still includes placeholder routes and disabled destinations. Actual routes/trust must be verified before a public E2E run |
| Intake, Income and Expense | Review/update/reject/confirm paths and owner validation; [core flow](../architecture/financial-core-e2e-flow.md) distinguishes confirmation intent from authoritative record commit |
| Receipt Processing, encrypted storage, OCR and Intake publisher | [Receipt pipeline](receipt-upload-ocr-pipeline.md), actual configured transport and authenticated consumer; generated image and fake OCR are the offline lane, not real-provider acceptance |
| Analytics, Summary, Score and Recommendations | Compatible owner-scoped lifecycle consumers, settings and freshness; configured actual adapters/broker must be evidenced, not inferred from in-memory tests |
| Notification preparation, delivery, permissions and client | Actual event producer/consumer, preferences, channel registration and receiver; [notification contract](../api/recommendations-notifications-v1.md) prepares delivery but does not establish a real send |
| React Native and platform services | Same candidate on Android and iOS with approved gateway, secure session, camera/picker and permissions; Node/TypeScript checks are not native execution |

Existing [CoreReleaseFlowTests](../../tests/FinancialAssistant.Release.Tests/CoreReleaseFlowTests.cs)
cover a bounded test-host path from registration to text draft, expense, Analytics
and score. They use synthetic trusted headers, in-memory adapters and explicit
projector/event handoff; they do not prove public gateway authentication, receipt
processing, Profile onboarding, device behavior or notification receipt. Future
FIN-204 extends executable E2E coverage. This ticket does not activate routes,
install adapters, implement a sender or mark any runtime gap passed.

Record the candidate source, reviewed head, actual merge, artifact/build mapping,
configuration reference, environment, OS/device, owners, UTC time, lane and safe
case evidence before execution. Run each mobile path independently on Android
and iOS, including the physical-device evidence required by the mobile plan.
A service-host simulation may be recorded at L2; only the approved connected
public gateway plus native journey can satisfy L3/L4 acceptance. A missing route,
real sender, trust configuration or required consumer makes that lane Blocked.

All data must be synthetic. Default additional spend is zero: no paid provider,
send, build, deployment or credit use is authorized. A generated image and fake
provider may test contracts; provider/channel sandbox runs require separate
owner, privacy, credential and maximum-spend approval. Never switch a failed
simulation to a live provider. Keep raw input/image/OCR/prompt/token contents
out of evidence and logs; use safe case IDs, outcomes and restricted references.

## Shared Fixture And Financial Oracle

Use USD, UTC and local reference date D, with no other confirmed records. The
clock-controlled test fixture uses D = 2026-08-20. On an approved host derive D
from the actual current UTC date and record it; do not change the host clock.
Abort/reinitialize the time-sensitive case if the date rolls over. Every amount
below is invented; the backend decimal/currency/date policy remains authoritative.

Set locale en-US, timezone UTC, default currency USD and completed onboarding
using Profile APIs. Configure monthly budget 50.00 through the supported settings
path, and verify the owner services consume that setting. Use supported active
expense and income categories from the actual taxonomy, not arbitrary labels.
Notifications are explicitly opted in for the approved test channel only.

| Stage | New authoritative contribution on D | Income total | Expense total | Balance delta |
| --- | --- | ---: | ---: | ---: |
| Empty / unconfirmed draft | None | 0.00 | 0.00 | 0.00 |
| Text expense confirmed | Expense 42.15 | 0.00 | 42.15 | -42.15 |
| Receipt expense confirmed | Expense 17.85 | 0.00 | 60.00 | -60.00 |
| Income confirmed | Income 100.00 | 100.00 | 60.00 | 40.00 |

With all three records on D and no other activity, the same totals apply to the
containing UTC day, Monday-based week and calendar month. Draft/reject/replay
variants add zero contribution. EUR and owner B remain separate; no implicit
conversion or combined total. Balance delta is period income minus expense,
not a claim about an external bank balance. The manifest stores this fixture
and expected final totals; repository tests check its arithmetic, not runtime
behavior. A user confirmation response alone never proves those totals converged.

## Scenario Catalog

### MVP-E2E-001 Register And Onboard

Preconditions: empty owner fixtures, working public Identity/Profile/Category
paths, explicit synthetic credentials supplied privately, no financial records.
Register A through the client; complete currency/locale/timezone/privacy and
budget settings; explicitly choose notification preference. Sign out, sign in,
relaunch and read the persisted profile. Onboarding flags and session state must
match backend state; empty dashboard cannot invent transactions. Register B
separately for isolation tests.

Negatives: invalid credentials return safe errors without account enumeration;
expired/revoked sessions require authentication. Invalid settings preserve prior
values. Interrupted onboarding resumes the unfinished step. A delayed registration
event or missing Profile cannot be reported as completed onboarding. A caller's
forged owner/trust headers must not grant another account's scope.

### MVP-E2E-002 Add Expense By Text

Preconditions: A onboarded, Intake review/update/confirm routes available, seeded
expense category. Submit generated text using a stable idempotency key. Read the
draft, explicitly correct/review amount 42.15, USD, D and expense category; do not
assume an offline parser inferred them. Before confirmation totals stay zero.
Confirm; observe stable transaction identity, owner commit and eventual one-time
projection of 42.15. Re-read via client without a second write.

Negatives: same key/input returns the same draft; different input with that key
conflicts and preserves it. Missing/ambiguous/low-confidence values stay reviewable
and unconfirmable until corrected. Stale revision update cannot overwrite newer
values. Reject a separate draft and verify zero contribution. Race/retry confirm
or lose its response: reload current state and recover the same transaction;
never compensate by posting a second expense. B cannot read or confirm A's draft.

### MVP-E2E-003 Upload Receipt And Confirm Draft

Preconditions: A, approved generated JPEG/PNG/WebP, encryption/configured metadata
store, OCR lane and authenticated OCR-to-Intake consumer. Exercise both camera
and picker on each required platform. Upload the generated receipt, observe its
owner-scoped status, then retrieve its reviewable draft. In the controlled fake
lane the generated candidate has 17.85 USD on D; review/correct it regardless of
confidence, then confirm once. Only the owner commit adds 17.85, for spend 60.00.

Negatives: picker cancel, denied permission, signature/media mismatch and file
over 10 MiB do not create a usable financial record. Timeout/cancellation and
unavailable OCR give truthful recovery without fake extraction or raw provider
errors. Ambiguous totals require correction. Replayed OCR completion keeps one
draft; repeated confirmation keeps one contribution. A second intentional upload
is not assumed content-deduplicated. Foreign receipt/draft access is denied without
disclosure; backgrounding/navigation cancels stale polling without false success.

### MVP-E2E-004 Add Income

Preconditions: same A, an active income category and Income owner available.
Use the supported client income capture/review flow for 100.00 USD on D. Verify
income type/category before confirmation. Observe one Income-owned record and
event, no Expense-owned contribution. After projection income is 100.00, expense
60.00 and balance delta 40.00; another currency remains unchanged.

Negatives: zero/negative amount, unsupported currency, mismatched category/type,
foreign owner and rejected/incomplete draft cannot create income. Lost response
and replay preserve one income identity. Delayed owner processing shows pending
state rather than prematurely claiming a durable record or new balance.

### MVP-E2E-005 Dashboard And Analytics

Preconditions: all three committed records; configured Summary/Analytics consumers
and public read routes. Query the USD/UTC/D scope, compare daily/weekly/monthly
totals with the shared fixture, then open category breakdown and empty periods.
Category contributions reconcile to backend totals; the client only formats
facts. Switch period/currency/account and refresh; late responses must not replace
the selected scope or expose A's data to B.

Negatives: hold event delivery, duplicate/reorder owning lifecycle events and
simulate read unavailability in the approved lane. Preserve last-known/stale
state; no duplicate total or fabricated healthy zero. B's empty response contains
no A contribution; EUR is isolated. Poll reads with bounded backoff until expected
values and applicable freshness/checkpoint prove convergence. Never use a fixed
sleep as proof. The owner sets and records the test deadline before execution;
timeout is Fail with last safe checkpoint, not automatic reconfirmation.

### MVP-E2E-006 Score And Recommendation

Preconditions: compatible financial/analytics/score event consumers and Profile
settings, current formula version, public score/recommendation reads. Open score,
factors/history and recommendation detail after processing the shared fixture.
Compare score/factors to the versioned deterministic backend policy, not a hardcoded
AI answer; [score contract](../api/financial-score-v1.md) defines neutral 50 for
an empty user, which is not the expected score after these transactions.
For configured monthly budget 50 and monthly spend 60, require the deterministic
`monthly-budget-nearing-limit` code with 120% usage; other valid codes may coexist.
The [rule generator](../../backend/services/recommendations-notifications/FinancialAssistant.RecommendationsNotifications.Domain/RecommendationGenerator.cs)
and [insights plan](insights-validation-test-plan.md) define facts and thresholds.

Negatives: disabled/failed wording provider uses deterministic text without
changing codes, severity, facts or score. Duplicate/stale events do not regress
accepted state. Missing settings remain explicit, not guessed budget. Empty,
partial and failed reads display truthful states; read/dismiss/replayed actions
preserve lifecycle. Another owner's IDs cannot expose details or change status.

### MVP-E2E-007 Receive Notification

Preconditions: approved event-driven trigger/producer/consumer, matching opted-in
preference, native permission, channel registration, sending adapter and actual
test receiver. Use a fresh approved synthetic notification occurrence generated
by the owning business flow; observe preparation, authorized delivery attempt,
receiver receipt and client inbox/read behavior. Link safe occurrence/correlation
metadata to receipt evidence. Replay the same occurrence: no duplicate send or
inbox entry; mark-read replay preserves the first timestamp.

Negatives: preference opt-out, OS denial, missing registration, quiet hours and
wrong owner suppress or defer according to the declared policy, never claim
delivery. Transient failure respects bounded attempts and existing spend budget;
permanent/unknown failure terminates safely. Lock-screen content contains no
amounts, owner identifiers, category, receipt or prompt content. A queued/prepared
record, terminal-status API update, fake sender success or provider acceptance
alone is not receiver receipt evidence. Never manually mark delivered to pass.
Current non-sending placeholders make the real receive lane Blocked; offline
contract success must remain separately labeled, not an E2E acceptance pass.

## Results And Exit

Record each ID and each negative variant separately with platform/lane, owner,
candidate/artifact/environment, UTC time, safe expected/actual result, restricted
evidence reference/hash and linked defect. Run replay/isolation/privacy variants
on both required platforms where client behavior is involved. Clear isolated
fixtures through the approved lifecycle; do not wipe shared stores or production.

Only an explicit Pass with valid evidence qualifies. Blank, Fail, Blocked and
Not run are not passing results. Every required happy path and negative variant
must pass at its declared level; a service simulation cannot replace a blocked
native/public-gateway/provider lane. Use QA P0/P1 blockers and approved P2 handling
from FIN-201; do not waive missing coverage by renaming severity. Rerun affected
flows after source/build/configuration/trust changes and re-evaluate the complete
candidate. FIN-201, P6/P7/P8/P9 operational, privacy and rollout gates remain
independent. First-user testing remains **Not Ready** until all required evidence
and the release-owner decision exist; this definition closes none of those gaps.
