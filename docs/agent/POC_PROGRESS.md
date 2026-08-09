# POC Readiness Progress

Last updated: 2026-08-09T12:14:07+03:00

## Current Snapshot

POC readiness after FIN-138 closure is **70.9%**:
**139 of 196 canonical POC leaf tickets are Done**.

The POC is **not yet ready for first-user testing**. The percentage measures
completed backlog scope; it is not an estimate of elapsed time or a substitute
for the readiness gates below.

Latest canonical closure:

- FIN-138 - P6.T11 Define limits progress and streaks model
- delivery PR:
  https://github.com/Marachert/FinancialAssistant.Host/pull/147
- delivery merge commit:
  https://github.com/Marachert/FinancialAssistant.Host/commit/bd6abef22ae2e02d487a14b9f780a95994b4a576
- previous readiness: 138 / 196, or 70.4%
- current readiness: 139 / 196, or 70.9%
- change: +0.5 percentage points

Latest Jira closure:

- FIN-138 - limits progress and tracking streaks implemented and closed in Jira
- previous readiness: 138 / 196, or 70.4%
- current readiness: 139 / 196, or 70.9%
- change: +0.5 percentage points

FIN-122 is an exact later duplicate of FIN-121, FIN-125 is an exact later
duplicate of FIN-124, FIN-127 is an exact later duplicate of FIN-126, and
FIN-135 is an exact later duplicate of FIN-134, and FIN-220 is an exact later
duplicate of FIN-138. Closing these duplicates does not change the numerator or
denominator.

Current delivery:

- FIN-138 is Done with verified implementation PR #147 and Jira evidence comment
  10804
- deterministic confirmed-record daily, weekly, and monthly limit progress now
  includes explainable tracking streaks and explicit local-calendar reset rules
- canonical POC readiness is 139/196 (70.9%), a +0.5 percentage-point change
- parent FIN-27 remains In Progress at 14 of 20 canonical children Done (70.0%)
- first-user testing remains Not Ready
- the next expected unfinished canonical leaf is FIN-139, subject to a fresh Jira
  rank and duplicate check

## Epic Progress

| Epic | POC area | Done | Total | Progress |
| --- | --- | ---: | ---: | ---: |
| FIN-1 | P0 Product clarification and release scope | 3 | 3 | 100.0% |
| FIN-5 | P1 Architecture definition and technical governance | 17 | 17 | 100.0% |
| FIN-10 | P2 Repository, DevOps, and local platform foundation | 36 | 36 | 100.0% |
| FIN-14 | P3 API Gateway, authentication, and security foundation | 28 | 28 | 100.0% |
| FIN-18 | P4 Financial core backend services | 22 | 22 | 100.0% |
| FIN-23 | P5 AI orchestration and OCR automation | 19 | 19 | 100.0% |
| FIN-27 | P6 Analytics, score, recommendations, and notifications | 14 | 20 | 70.0% |
| FIN-31 | P7 Mobile app UX and React Native implementation | 0 | 18 | 0.0% |
| FIN-36 | P8 Observability, admin UI, audit, and MCP tooling | 0 | 13 | 0.0% |
| FIN-38 | P9 Testing, Windows deployment, and release readiness | 0 | 20 | 0.0% |
| **Total** | **Canonical POC leaf scope** | **139** | **196** | **70.9%** |

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

Current blocking areas are P6, P7, P8, and P9. In particular, P7 has no
completed canonical leaf tickets, so there is not yet a client experience that
can be handed to first users.

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
