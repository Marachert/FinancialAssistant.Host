# Insights Release Readiness Checklist

Related Jira: FIN-155.

## Purpose

Use this checklist for a candidate release of Analytics, Financial Score,
Recommendations, limits and streaks, Notifications, and explanation behavior.
It converts the P6 quality, privacy, contract, UX, and mobile dependencies into
explicit evidence and a release decision.

Completing P6 implementation checks does not by itself make the Financial
Assistant POC ready for first-user testing. P7 client UX, P8 operations, and P9
integration, deployment, and release gates remain independently blocking.

## Decision rules

- Record the exact candidate commit and exact-head CI run. Evidence from an
  earlier head is invalid.
- Mark every item as `Pass`, `Fail`, `Blocked`, or `Not applicable`.
  `Not applicable` requires a written reason and approver.
- Any `Fail`, unexplained `Not applicable`, missing required evidence, or
  blocking item in this document prevents release.
- A deferred capability may be outside a narrowly declared P6 contract release,
  but it must remain visible and cannot be represented as production-ready.
- Use synthetic data only. Do not attach identities, financial records, receipt
  content, prompts, tokens, credentials, or provider payloads.
- AI, OCR, and wording-provider output is never a correctness oracle. Backend
  facts, calculations, lifecycle state, and privacy policy are authoritative.

## Candidate record

| Field | Required value |
| --- | --- |
| Release name/version | |
| Candidate commit SHA | |
| GitHub pull request | |
| Backend CI run | |
| Validation date in UTC | |
| Reviewer | |
| Jira evidence | |
| Confluence evidence | |
| Declared release boundary | P6 contract / internal POC / first-user POC / production |
| Final decision | Pass / Blocked |
| Decision rationale | |

## 1. Baseline verification

- [ ] Restore, Release build, all solution tests, and format verification pass.
- [ ] Protected Backend CI succeeds on the exact candidate SHA.
- [ ] No required check is pending, cancelled, or unexpectedly skipped.
- [ ] The matrix and deterministic examples in
  `docs/engineering/insights-validation-test-plan.md` pass unchanged.
- [ ] No test is weakened, disabled, or made dependent on AI/OCR/provider text.
- [ ] Review threads, submitted reviews, conversation comments, blocking labels,
  and mergeability have been checked on the current head.
- [ ] The diff contains no secret, production configuration, real user data,
  generated binary, or unrelated scope.

Evidence:

| Check | Status | Link or safe result |
| --- | --- | --- |
| Restore/build/test/format | | |
| Exact-head Backend CI | | |
| Validation plan | | |
| Review and scope audit | | |
| Privacy scan | | |

## 2. Dashboard API contract review

Analytics service routes:

- [ ] `GET /api/v1/analytics/dashboard` and `GET /analytics/dashboard`
  remain compatible with `docs/api/analytics-dashboard-v1.md`.
- [ ] Daily, Monday-based weekly, and calendar-month boundaries are explicit.
- [ ] Summary, category, trend, limit, streak, and Analytics freshness fields
  are stable or carry an approved version change and migration note.
- [ ] Empty periods return zero-safe values and empty collections.
- [ ] Missing limits stay explicitly unconfigured; callers cannot invent or
  submit authoritative limits through the dashboard request.
- [ ] Internal owner hashes, record/event IDs, revisions, origins, and storage
  details remain absent from the Analytics response.

Dashboard composition contract:

- [ ] `DashboardCompositionResponse` mock data matches
  `docs/api/dashboard-composition-v1.md`, including score, recommendation,
  notification, empty-state, and per-source freshness fields.
- [ ] Review evidence explicitly records that the composition contract is
  mock-only and has no activated public route.
- [ ] No client treats composition as runtime-available until a future endpoint
  preserves service ownership, activates its gateway destination, and passes
  contract and end-to-end tests.
- [ ] Mock responses used by the mobile team identify the Analytics service
  response and dashboard composition response as separate contracts.

Evidence owner: Analytics, composition, and mobile contract reviewers.

## 3. Analytics correctness review

- [ ] Only active confirmed Income and Expense lifecycle events affect totals.
- [ ] Archive, restore, duplicate, and out-of-order revisions are idempotent.
- [ ] Currency moves update both affected scopes exactly once.
- [ ] Owner, currency, local date, week, and month isolation is verified.
- [ ] Category shares, top-N ordering, uncategorized fallback, percentages, and
  rounding match documented deterministic rules.
- [ ] Daily, weekly, and monthly limits use authoritative settings and
  non-negative remaining values.
- [ ] Tracking streak resets and timezone conversion are explainable and tested.
- [ ] Empty, never-built, delayed, and stale projections expose honest state.
- [ ] Durable storage limitations are declared for the selected release boundary.

Evidence owner: Analytics reviewer.

## 4. Financial Score formula review

- [ ] The candidate declares the formula version, currently
  `financial-score-v2`, and a score range of 0 through 100.
- [ ] Repeated calculation with identical facts is byte-equivalent and
  independent of input ordering.
- [ ] Factor order, contribution, cap/penalty policy, and total are explainable.
- [ ] New users receive the documented neutral score and zero contributions.
- [ ] Archived and out-of-window records do not affect the score.
- [ ] History ordering, inclusive period filters, cursor behavior, and owner /
  currency isolation are verified.
- [ ] No semantic adjustment, LLM, OCR, or wording provider can calculate,
  modify, or override a contribution or final score.
- [ ] Any formula change has a new version, reviewed examples, migration impact,
  and client wording update.

Evidence owner: Financial Score reviewer.

## 5. Recommendation wording and explainability review

- [ ] Every recommendation is traceable to a deterministic rule code and
  structured backend facts.
- [ ] Stable identity prevents a replay from creating duplicate recommendations.
- [ ] Read, dismiss, expire, and terminal lifecycle behavior is verified.
- [ ] The explanation includes a localization key, evidence confidence, safe
  deterministic fallback, and allowlisted mobile action metadata.
- [ ] Optional wording can improve display text only and cannot add facts,
  amounts, urgency, products, or financial claims.
- [ ] Missing, invalid, timed-out, or failed wording providers use fallback text;
  caller cancellation still propagates.
- [ ] Wording is clear, non-invasive, non-judgmental, and does not present a
  recommendation as professional financial advice.
- [ ] Localization review preserves the deterministic meaning and action.

Evidence owner: Recommendations, product-content, and localization reviewers.

## 6. Notification frequency and preference review

- [ ] The six MVP trigger types have documented conditions and stable occurrence
  keys: daily input, approaching limit, exceeded limit, score improved,
  recommendation available, and receipt processing completed.
- [ ] Replays cannot publish a second notification for the same occurrence.
- [ ] Channel and notification-type opt-outs are evaluated before template
  preparation, storage, or publication.
- [ ] New-owner defaults are reviewed for user expectations and consent.
- [ ] Approaching and exceeded thresholds cannot create contradictory or noisy
  messages for one occurrence.
- [ ] Daily reminder cadence and local-date behavior are reviewed.
- [ ] Quiet-hours configuration is validated. If scheduling is not implemented,
  that limitation is explicitly blocking for any boundary that requires it.
- [ ] Retry applies only to explicitly transient provider failures and is
  bounded; permanent configuration failures do not retry.
- [ ] Delivery status never claims success from a non-sending placeholder.

Evidence owner: Notifications and product reviewer.

## 7. Lock-screen privacy review

Inspect every push and web template and at least one payload per trigger type.

- [ ] Title and body contain no amount, balance, income, expense, category,
  merchant, receipt content, owner/user identifier, or source event ID.
- [ ] Payload metadata contains no raw financial fact, prompt, OCR text,
  credential, endpoint, or provider response.
- [ ] Generic wording does not reveal a sensitive financial condition to a
  person viewing a locked device.
- [ ] Deep-link/action metadata uses an allowlist and requires authenticated
  in-app retrieval of details.
- [ ] Logs, failure diagnostics, retries, and dead-letter evidence remain
  privacy-safe.
- [ ] Screenshots and test evidence use synthetic data only.

Evidence owner: Security/privacy reviewer.

## 8. Mobile dependency review

- [ ] Mobile has approved mock and runtime contracts for dashboard, score,
  recommendations, notification badge, and preferences.
- [ ] Loading, empty, stale, unavailable, partial-source, retry, and error states
  are designed and testable.
- [ ] Score factors and recommendation reasons are readable and accessible.
- [ ] Recommendation actions are allowlisted and route to an authenticated view.
- [ ] Notification settings expose channel/type choices and honest quiet-hours
  behavior.
- [ ] Local timezone, number, currency, and localization rendering is verified.
- [ ] Authentication and trusted gateway integration work without exposing
  internal headers or owner hashes.
- [ ] End-to-end synthetic flow covers confirmed transaction to refreshed
  dashboard/score/recommendation/notification state.
- [ ] The mobile dependency owner records the P7 ticket and status for every
  incomplete item.

Evidence owner: Mobile reviewer.

## 9. Rebuild, operations, and support review

- [ ] The selected boundary states whether rebuild/backfill is executable or
  contract-only.
- [ ] A production-capable rebuild proves durable checkpoints, source high-water
  replay, staging isolation, owner/period atomic swap, downstream refresh, and
  preservation of the active projection on failure.
- [ ] Rebuild initiation is restricted to a trusted admin path and emits safe
  progress/failure evidence.
- [ ] Monitoring distinguishes source lag, stale projection, consumer failure,
  delivery failure, and configuration failure without exposing financial data.
- [ ] Support diagnostics include correlation and version data sufficient to
  reproduce a synthetic case.
- [ ] Rollback or disable controls exist for formula, recommendation,
  notification, and provider changes in the declared boundary.
- [ ] Operational ownership and escalation links are recorded.

Evidence owner: Operations and support reviewers.

## 10. Current known blocker baseline

The following repository facts are blockers unless a release boundary explicitly
and honestly excludes the capability:

| Capability | Current repository state | Release impact |
| --- | --- | --- |
| Analytics projection storage | In-memory single-instance POC adapter | Blocks durable/production claims |
| Financial Score storage/settings | In-memory POC adapters | Blocks durable/production claims |
| Recommendation/notification storage | In-memory and not crash-durable | Blocks durable/production claims |
| Push and web delivery | Non-sending provider-neutral placeholders | Blocks real notification delivery |
| Quiet hours | Contract placeholder; no deferred scheduler | Blocks a claim that quiet hours are enforced |
| Rebuild/backfill | Planner and contract only; no trusted admin executor or atomic durable swap | Blocks operational recovery claims |
| Mobile first-user flow | P7 readiness must be verified independently | Blocks first-user POC release while incomplete |
| Monitoring/admin/audit | P8 readiness must be verified independently | Blocks supported first-user/production release while incomplete |
| End-to-end deployment/release | P9 readiness must be verified independently | Blocks first-user POC release while incomplete |

A reviewer may add blockers but must not delete an unresolved blocker to obtain a
passing decision. Close a blocker only with a ticket, exact-head evidence, and
an updated checklist row.

## 11. Blocker register

| Blocker | Severity | Owning Jira ticket | Owner | Required evidence | Status |
| --- | --- | --- | --- | --- | --- |
| | | | | | |

## 12. Sign-off

All required reviewers must sign the same exact candidate SHA.

| Review area | Reviewer | Decision | Evidence | Recorded at UTC |
| --- | --- | --- | --- | --- |
| Analytics/contracts | | | | |
| Financial Score | | | | |
| Recommendations/content | | | | |
| Notifications/frequency | | | | |
| Security/privacy | | | | |
| Mobile UX | | | | |
| Operations/support | | | | |
| Release owner | | | | |

Final release outcome:

- [ ] **Pass**: every required item passes, the blocker register is empty, and
  every required sign-off references the exact candidate SHA.
- [ ] **Blocked**: one or more required items, dependencies, or sign-offs remain
  open. Record the blockers above and do not describe the candidate as ready.

## Related documents

- `docs/engineering/insights-validation-test-plan.md`
- `docs/engineering/analytics-dashboard-read-model.md`
- `docs/engineering/analytics-rebuild-backfill.md`
- `docs/engineering/financial-score-v1.md`
- `docs/engineering/recommendations-notifications-v1.md`
- `docs/engineering/notification-delivery-adapters.md`
- `docs/api/analytics-dashboard-v1.md`
- `docs/api/dashboard-composition-v1.md`
- `docs/api/financial-score-v1.md`
- `docs/api/recommendations-notifications-v1.md`
- `docs/api/notification-preferences-v1.md`
