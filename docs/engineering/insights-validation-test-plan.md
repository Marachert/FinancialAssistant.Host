# Insights Validation Test Plan

Related Jira: FIN-152.

## Purpose

This plan defines release evidence for P6 Analytics, Financial Score,
Recommendations, limits and streaks, Notifications, explanation wording, and
rebuild/backfill behavior. All examples use synthetic owners, events, settings,
and amounts.

A passing plan demonstrates deterministic backend correctness. It does not by
itself make the POC ready for first-user testing; mobile, observability,
deployment, privacy, and end-to-end gates remain separate.

## Test principles

- Confirmed Income and Expense records and versioned lifecycle events are the
  only financial source of truth.
- Tests assert backend codes, facts, calculations, state, and contracts.
- Tests never use AI or OCR output as an expected-value oracle.
- Optional wording providers are unavailable or synthetic stubs in tests.
- Owner, currency, date/time-zone, revision, and lifecycle boundaries are
  explicit in every relevant scenario.
- Replays and retries must be idempotent.
- Examples must not contain real identity, financial, receipt, prompt, token, or
  provider data.

## CI baseline

Run the full backend gate for a release candidate:

```powershell
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release --logger trx --results-directory TestResults
dotnet format FinancialAssistant.Backend.sln --verify-no-changes --verbosity diagnostic
```

The exact candidate head must also pass the protected GitHub Backend CI workflow.
A failed, cancelled, pending, or unexpectedly skipped check blocks release.

## Coverage map

| Area | Primary executable suites | Required evidence |
| --- | --- | --- |
| Daily/weekly/monthly summaries | `AnalyticsProjectorTests`, `AnalyticsEndpointTests`, `AnalyticsContractTests` | confirmed-only totals, zero-safe empty periods, Monday week, calendar month, archive/restore, currency and owner isolation, freshness |
| Category breakdown | `AnalyticsProjectorTests`, `AnalyticsEndpointTests`, `AnalyticsContractTests` | daily/weekly/monthly periods, independent income/expense shares, deterministic ranking, top-N, uncategorized fallback |
| Limits and streaks | `AnalyticsProjectorTests`, `AnalyticsEndpointTests` | configured/unconfigured limits, remaining and used percentage, over-limit values, local-date resets, gaps, latest tracked date |
| Financial Score | `FinancialScoreCalculatorTests`, `FinancialScoreServiceTests`, `FinancialScoreEndpointTests` | versioned formula, bounded score, factor inputs/contributions, history ordering, new-user default, archived/out-of-window exclusion |
| Recommendation rules | `RecommendationGeneratorTests`, `RecommendationNotificationServiceTests`, `RecommendationNotificationEndpointTests` | deterministic trigger codes/facts, stable IDs, deduplication, owner scope, read/dismiss/expire lifecycle |
| Recommendation explanations | `RecommendationExplanationServiceTests` | deterministic fallback, localization key, evidence confidence, allowlisted action, bounded wording-only enhancement, provider timeout fallback |
| Notification preferences | `RecommendationNotificationServiceTests`, `RecommendationNotificationEndpointTests` | defaults, channel/type opt-out before preparation/publication, quiet-hours validation and owner isolation |
| Notification triggers | `RecommendationNotificationServiceTests` | reminder, approaching/exceeded limit, score improvement, recommendation and receipt completion, stable occurrence deduplication, lock-screen-safe text |
| Notification delivery | `NotificationDeliveryAdapterTests` | disabled suppression, missing-configuration failure, non-sending placeholders, terminal status, bounded retry eligibility |
| Rebuild/backfill | `AnalyticsRebuildPlannerTests`, `AnalyticsProjectorTests` | stable owner/period/source job key, bounded validation, stage order, safe failure contract, revision replay and deterministic aggregates |

## Deterministic expected examples

### Summary and categories

Given active confirmed records for one synthetic owner and USD:

| Record | Date | Amount | Category |
| --- | --- | ---: | --- |
| Income | 2026-08-20 | 1,000 | `income.salary` |
| Expense | 2026-08-20 | 100 | `expense.groceries` |
| Expense | 2026-08-20 | 75 | `expense.utilities` |
| Expense | 2026-08-20 | 50 | `uncategorized` |
| Expense | 2026-08-10 | 25 | `expense.entertainment` |

Expected daily values for 2026-08-20 are income 1,000, expense 225, and balance
delta 775. Groceries are 44.44% of daily expenses, utilities are 33.33%, and
uncategorized is 22.22%, rounded midpoint-away-from-zero. The August monthly
expense total is 250. A replay of the same revision changes none of these
values. Archiving groceries with a newer revision removes 100; restoring it
with a later revision adds exactly 100 once.

### Limits and streaks

With daily, weekly, and monthly limits of 50, 120, and 300 and confirmed spend
of 40 today and 60 earlier in the same Monday-based week:

- daily used is 80.00%, remaining is 10;
- weekly used is 83.33%, remaining is 20;
- monthly used is 33.33%, remaining is 200;
- a confirmed record on each consecutive local date increments the streak;
- a gap before the reference date resets current streak to zero but retains the
  latest tracked date;
- missing settings return `isConfigured = false` and never invent a limit.

### Financial score

For synthetic current-month income 1,000 and expense 400, previous-month income
1,000 and expense 500, complete Profile settings, and monthly budget 1,000:

- repeated calculation with records in any order returns byte-equivalent
  versioned snapshots;
- factors appear in stable order: `budget_usage`, `spending_trend`,
  `income_consistency`, `data_completeness`, `penalty_cap`;
- the result remains inside the formula minimum and maximum;
- an expense-only user is penalized and capped at 39 or lower;
- a new user with no records receives the documented neutral default and zero
  factor contributions.

### Recommendations and notifications

For monthly income 1,000, expense 1,050, daily limit 50, daily spend 60, and
score 42, expected recommendation codes are:

```text
daily-limit-reached
negative-cash-flow
score-recovery
```

The expense-to-income fact is 105.00%. Replaying the same source event creates
no second recommendation set or notification occurrence. With push disabled
and web enabled, only web preparations are stored and published. Lock-screen
text must not contain amounts, owner scope, category, receipt content, or source
event IDs.

## New-user and empty-state cases

The release candidate must verify:

- empty analytics periods return zero totals and empty category collections;
- never-built or delayed projections expose stale/unavailable metadata;
- unconfigured limits remain nullable and explicitly unconfigured;
- Financial Score returns the versioned neutral new-user default;
- Recommendation rules use a non-invasive steady-course fallback when no risk
  signal exists;
- missing Profile settings do not produce a false incomplete-profile signal;
- Notification preferences use documented new-owner defaults;
- unavailable explanation wording uses deterministic fallback text;
- a rebuild request with no source records can succeed with zero processed
  records and an empty staged scope.

## Negative, replay, and privacy cases

Required negative coverage includes:

- unsupported event type/version, missing owner scope, invalid currency,
  non-positive amount, invalid status/date, and negative revision;
- duplicate and out-of-order lifecycle events;
- a record moving currency and publishing both affected scopes exactly once;
- stale status timestamps and forbidden terminal lifecycle transitions;
- invalid score settings and out-of-window or archived score inputs;
- invalid recommendation wording and provider-only timeout;
- caller cancellation propagation;
- invalid notification type, channel, quiet-hours zone/window, and delivery
  transition;
- transient delivery retry versus permanent failure;
- malformed rebuild owner hash, reversed/oversized period, non-UTC request,
  changed source version, and safe failed-stage serialization;
- absence of owner hashes, raw payloads, amounts, prompts, credentials, and
  provider endpoints from public/error/progress responses where prohibited.

## Rebuild acceptance

The checked-in planner tests validate contracts and stable job identity. Before
a production rebuild executor can be released, integration coverage must also
prove durable checkpoint resume, source high-water replay, staging isolation,
atomic owner/period swap, no cross-owner deletion, downstream score/limit/
recommendation refresh, and active-projection preservation on failure. Those
tests are blocked until the trusted admin and durable adapters exist; the
development `ResetAsync` helper is not acceptable evidence.

## Evidence record

For every release candidate, retain:

- exact git head and merge commit;
- restore/build/test/format command results;
- TRX artifacts and GitHub workflow URL/run number;
- failing test name and safe diagnostic for every retry;
- review-thread resolution evidence;
- Jira and Confluence links;
- explicit list of deferred integration cases and owning ticket.

## Exit criteria

P6 validation is acceptable only when:

- every existing suite in the coverage map passes on the exact head;
- no deterministic expected example differs without a reviewed formula or
  contract version change;
- all negative, new-user, replay, owner/currency, and privacy cases pass;
- no test is weakened, skipped, or made dependent on AI wording;
- no unresolved actionable review or CI blocker remains;
- deferred durable rebuild/admin and external delivery tests remain visibly
  blocked from release claims until their dependencies exist.
