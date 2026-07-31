# POC Readiness Progress

Last updated: 2026-07-31T12:25:54+03:00

## Current Snapshot

POC readiness after FIN-65 closure is **52.6%**:
**103 of 196 canonical POC leaf tickets are Done**.

The POC is **not yet ready for first-user testing**. The percentage measures
completed backlog scope; it is not an estimate of elapsed time or a substitute
for the readiness gates below.

Latest canonical closure:

- FIN-65 - Implement shared integration event envelope
- delivery PR:
  https://github.com/Marachert/FinancialAssistant.Host/pull/77
- merge commit:
  https://github.com/Marachert/FinancialAssistant.Host/commit/b504ba690e9c0b61455afcbd6294cdbcf298c045
- previous readiness: 102 / 196, or 52.0%
- current readiness: 103 / 196, or 52.6%
- change: +0.6 percentage points

FIN-122 is an exact later duplicate of FIN-121, and FIN-125 is an exact later
duplicate of FIN-124. Closing either duplicate does not change the numerator or
denominator.

Current delivery:

- FIN-65 is Done after PR 77 merged the shared .NET 8 integration-event
  envelope with a strongly typed generic payload
- required event, occurrence, version, producer, correlation, causation, user
  hash, and UTC occurrence metadata are validated at construction time
- operational identifiers are bounded and control-free, event-type/schema
  versions must align, and uninitialized occurrence timestamps are rejected
- Backend CI #257 passed restore, Release build, tests, and format on the final
  FIN-65 head
- parent FIN-63 is Done after FIN-64 and FIN-65 were both verified Done

## Epic Progress

| Epic | POC area | Done | Total | Progress |
| --- | --- | ---: | ---: | ---: |
| FIN-1 | P0 Product clarification and release scope | 3 | 3 | 100.0% |
| FIN-5 | P1 Architecture definition and technical governance | 17 | 17 | 100.0% |
| FIN-10 | P2 Repository, DevOps, and local platform foundation | 32 | 36 | 88.9% |
| FIN-14 | P3 API Gateway, authentication, and security foundation | 28 | 28 | 100.0% |
| FIN-18 | P4 Financial core backend services | 4 | 22 | 18.2% |
| FIN-23 | P5 AI orchestration and OCR automation | 19 | 19 | 100.0% |
| FIN-27 | P6 Analytics, score, recommendations, and notifications | 0 | 20 | 0.0% |
| FIN-31 | P7 Mobile app UX and React Native implementation | 0 | 18 | 0.0% |
| FIN-36 | P8 Observability, admin UI, audit, and MCP tooling | 0 | 13 | 0.0% |
| FIN-38 | P9 Testing, Windows deployment, and release readiness | 0 | 20 | 0.0% |
| **Total** | **Canonical POC leaf scope** | **103** | **196** | **52.6%** |

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

Current blocking areas are P4, P6, P7, P8, and P9. In particular, P7 has no
completed canonical leaf tickets, so there is not yet a client experience that
can be handed to first users.

## Calculation Contract

This file is recalculated after every canonical ticket is closed.

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

| Recorded at | Canonical ticket | Result | POC readiness | Change |
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
