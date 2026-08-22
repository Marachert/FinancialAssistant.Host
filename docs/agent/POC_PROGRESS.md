# POC Readiness Progress

Last updated: 2026-08-22T19:35:00+03:00

## Current Snapshot

POC readiness after FIN-159 duplicate closure is **76.6%**:
**151 of 197 canonical POC leaf tickets are Done**.

The POC is **not yet ready for first-user testing**. The percentage measures
completed backlog scope; it is not an estimate of elapsed time or a substitute
for the readiness gates below.

Latest canonical closure:

- FIN-158 - P7.T1 Define mobile UX scope and primary user flows
- delivery PR:
  https://github.com/Marachert/FinancialAssistant.Host/pull/176
- delivery merge commit:
  https://github.com/Marachert/FinancialAssistant.Host/commit/5ef63418716652a37c0a1202fb6e635a2b66a280
- previous readiness: 150 / 197, or 76.1%
- current readiness: 151 / 197, or 76.6%
- change: +0.5 percentage points

Latest Jira closure:

- FIN-159 - Exact later duplicate of canonical FIN-158, Jira-linked and Done
- implementation PR:
  https://github.com/Marachert/FinancialAssistant.Host/pull/176
- implementation merge commit:
  https://github.com/Marachert/FinancialAssistant.Host/commit/5ef63418716652a37c0a1202fb6e635a2b66a280
- final implementation head: `ddd738c7ae2b05e9f4642bb2173789dd37e5f4fd`
- exact-head Backend CI: #509
- previous readiness: 151 / 197, or 76.6%
- current readiness: 151 / 197, or 76.6%
- change: +0.0 percentage points

FIN-122 is an exact later duplicate of FIN-121, FIN-125 is an exact later
duplicate of FIN-124, FIN-127 is an exact later duplicate of FIN-126, and
FIN-135 is an exact later duplicate of FIN-134, FIN-220 is an exact later
duplicate of FIN-138, and FIN-141, FIN-142, and FIN-143 are exact later
duplicates of FIN-140, FIN-146 is an exact later duplicate of FIN-145, and
FIN-148, FIN-149, FIN-150, and FIN-151 are exact later duplicates of FIN-147,
FIN-153 and FIN-154 are exact later duplicates of FIN-152, and FIN-156 and
FIN-157 are exact later duplicates of FIN-155. FIN-159 is an exact later
duplicate of FIN-158. Closing these duplicates does not change the numerator or
denominator.

Current delivery:

- FIN-159 is Done and Jira-linked as an exact duplicate of canonical FIN-158
- canonical implementation remains merged PR #176 at
  `5ef63418716652a37c0a1202fb6e635a2b66a280`, Backend CI #509 green
- FIN-160 and FIN-161 remain exact later duplicate candidates with the same
  parent, summary, description, scope, and Definition of Done
- canonical POC readiness remains 151/197 (76.6%), a +0.0 percentage-point change
- FIN-31 P7 progress is 5/18 (27.8%) and the epic remains In Progress because
  ranked implementation leaves remain unfinished
- FIN-27 remains In Progress because its event-driven notification delivery
  Definition of Done is not yet satisfied
- first-user testing remains Not Ready because additional mobile work and
  runtime P6, P8, and P9 gates remain open
- no paid provider or review credits were used
- the next ranked unfinished leaf is expected to be FIN-160 for duplicate
  resolution, subject to a fresh audit after this progress record merges

## Epic Progress

| Epic | POC area | Done | Total | Progress |
| --- | --- | ---: | ---: | ---: |
| FIN-1 | P0 Product clarification and release scope | 3 | 3 | 100.0% |
| FIN-5 | P1 Architecture definition and technical governance | 17 | 17 | 100.0% |
| FIN-10 | P2 Repository, DevOps, and local platform foundation | 36 | 36 | 100.0% |
| FIN-14 | P3 API Gateway, authentication, and security foundation | 28 | 28 | 100.0% |
| FIN-18 | P4 Financial core backend services | 22 | 22 | 100.0% |
| FIN-23 | P5 AI orchestration and OCR automation | 19 | 19 | 100.0% |
| FIN-27 | P6 Analytics, score, recommendations, and notifications | 21 | 21 | 100.0% |
| FIN-31 | P7 Mobile app UX and React Native implementation | 5 | 18 | 27.8% |
| FIN-36 | P8 Observability, admin UI, audit, and MCP tooling | 0 | 13 | 0.0% |
| FIN-38 | P9 Testing, Windows deployment, and release readiness | 0 | 20 | 0.0% |
| **Total** | **Canonical POC leaf scope** | **151** | **197** | **76.6%** |

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
- the Windows PoC deployment stack is repeatable and the first-user environment
  is verified.

Current blocking areas are runtime P6, P7, P8, and P9. P7 now has authentication,
free-form transaction capture, receipt upload, editable draft review, backend
confirmation, dashboard, score/recommendation, and settings screens. Remaining
ranked mobile work includes onboarding, charts, inbox, resilient offline states,
and release-ready end-to-end validation.

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
