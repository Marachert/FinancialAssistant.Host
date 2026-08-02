# Financial Core Validation and Edge-Case Test Plan

Related Jira: FIN-102.

## Objective

This plan is the implementation contract for deterministic validation across
Transaction Intake, Category, Income, Expense, financial record events, and
Financial Summary. Tests use synthetic data only. Backend rules, not AI/OCR output,
are authoritative for acceptance, persistence, lifecycle, and calculations.

A case is complete only when its positive and negative assertions are automated in
the named project, passes in the full Release solution run, and does not rely on a
live provider, broker, database, clock, locale, or network.

## Test layers and owners

| Layer | Target project | Responsibility |
| --- | --- | --- |
| Intake unit/integration | `FinancialAssistant.TransactionIntake.Tests` | draft validation, idempotency, confirmation, ambiguity |
| Category unit/integration | `FinancialAssistant.Category.Tests` | taxonomy shape, user aliases, ownership |
| Income service | `FinancialAssistant.Income.Tests` | owner-scoped CRUD, validation, lifecycle, totals, events |
| Expense service | `FinancialAssistant.Expense.Tests` | owner-scoped CRUD, validation, lifecycle, totals, events |
| Summary projection/contract | `FinancialAssistant.FinancialSummary.Tests` | event replay, active totals, periods, freshness, public shape |
| Cross-boundary architecture | `FinancialAssistant.Repository.Tests` | source-of-truth and dependency boundaries |
| End-to-end | future P9 integration suite | gateway-to-summary happy path and failure recovery |

Unit tests freeze `TimeProvider` and avoid HTTP. Service integration tests use
in-memory adapters and authenticated test hosts. End-to-end tests use local,
containerized free dependencies and synthetic fixtures.

## Amount validation

| ID | Scenario | Expected assertion |
| --- | --- | --- |
| FCV-001 | positive amount at minimum accepted precision | stored amount is positive and normalized |
| FCV-002 | zero or negative amount | `400 invalid_*_request`; no draft, record, event, or total change |
| FCV-003 | amount above service maximum | deterministic rejection before storage |
| FCV-004 | more than two decimal places | banker's rounding is stable and event/summary use the stored value |
| FCV-005 | amount that rounds to zero | rejected; no authoritative record |
| FCV-006 | repeated decimal and midpoint values | no binary floating-point conversion; exact decimal totals |
| FCV-007 | sum near supported aggregate boundary | no overflow; explicit failure if the supported bound is exceeded |
| FCV-008 | same value in two currencies | two independent totals; never an implicit conversion or combined total |

## Currency validation

| ID | Scenario | Expected assertion |
| --- | --- | --- |
| FCV-009 | lowercase supported code | normalized once to uppercase |
| FCV-010 | missing, whitespace, non-three-letter, or unsupported code | deterministic rejection with no mutation |
| FCV-011 | update changes currency | old revision no longer contributes; new currency contributes once |
| FCV-012 | summary query currency differs from records | zero-safe response for that currency |
| FCV-013 | parser suggests unsupported currency | suggestion remains reviewable but cannot be confirmed |
| FCV-014 | currency conversion requested implicitly | no conversion rate lookup and no cross-currency balance |

## Date, timezone, and period validation

| ID | Scenario | Expected assertion |
| --- | --- | --- |
| FCV-015 | default or out-of-range financial date | rejected before storage |
| FCV-016 | exact lower and upper supported date boundaries | boundary dates accepted; one day beyond rejected |
| FCV-017 | leap day | valid leap date persists and groups into the correct month |
| FCV-018 | daylight-saving gap and overlap | local reference date uses validated IANA zone; no duplicate day |
| FCV-019 | UTC event near local midnight | stored financial `DateOnly` controls totals, not broker arrival date |
| FCV-020 | Monday and Sunday weekly boundaries | Monday-based inclusive week is stable |
| FCV-021 | month/year boundary | December/January and month-end records group correctly |
| FCV-022 | invalid timezone or malformed reference date | `400 invalid_summary_query`; no hidden server-local fallback |

## Duplicate and concurrency behavior

| ID | Scenario | Expected assertion |
| --- | --- | --- |
| FCV-023 | same intake idempotency key and same payload | original draft response replayed |
| FCV-024 | same intake idempotency key and different payload | conflict; original draft unchanged |
| FCV-025 | concurrent duplicate confirmation | one authoritative record and one logical created event |
| FCV-026 | broker redelivers confirmation | Income/Expense store and outbox remain single-effect |
| FCV-027 | financial event redelivery | summary projection remains single-effect by revision/event identity |
| FCV-028 | newer event followed by older revision | latest projection and totals remain unchanged |
| FCV-029 | concurrent record updates | one compare-and-replace wins; loser retries or returns stable conflict |
| FCV-030 | rebuild replays events in a different input order | resulting projection checksum and totals are identical |

## Archive, restore, and correction

| ID | Scenario | Expected assertion |
| --- | --- | --- |
| FCV-031 | archive active record | revision advances; record excluded from default lists and every total |
| FCV-032 | archive already archived record | idempotent response; no new revision or event |
| FCV-033 | update archived record | conflict; archived value remains auditable |
| FCV-034 | restore archived valid record | revision advances and record contributes exactly once |
| FCV-035 | restore already active record | idempotent response; no duplicate event or total |
| FCV-036 | corrected amount/category/date/currency | immutable identity/origin preserved; only latest revision contributes |
| FCV-037 | archive/restore events delivered out of order | highest revision controls projection |
| FCV-038 | physical delete attempt | unsupported in first-release lifecycle |

## Category references

| ID | Scenario | Expected assertion |
| --- | --- | --- |
| FCV-039 | valid `income.*` and `expense.*` category | owning service accepts matching namespace |
| FCV-040 | cross-type category | rejected before persistence |
| FCV-041 | malformed, missing, mixed-case, or overlong category | normalized when allowed or rejected deterministically |
| FCV-042 | user alias changes | stable category ID remains unchanged in record/event/summary |
| FCV-043 | unknown future custom category | rejected until a versioned Category projection contract authorizes it |
| FCV-044 | category breakdown empty period | returns `[]`, not `null`, and zero totals |

## Ownership and privacy

| ID | Scenario | Expected assertion |
| --- | --- | --- |
| FCV-045 | other user reads or mutates a record | indistinguishable `404`; no existence leak |
| FCV-046 | same record ID under different owners in an adapter fixture | strict owner isolation |
| FCV-047 | untrusted client supplies gateway user header | gateway authentication rejects spoofing |
| FCV-048 | financial event payload inspection | user is pseudonymous; merchant, draft, raw intake, OCR/LLM, and credentials absent |
| FCV-049 | summary response inspection | owner hash, record/event IDs, revision, origin, and storage state absent |
| FCV-050 | logs for invalid input or publish failure | safe reason code only; no payload or personal financial data |

## Draft, confirmed, manual, and AI boundaries

| ID | Scenario | Expected assertion |
| --- | --- | --- |
| FCV-051 | incomplete or ambiguous parser result | draft requires review and cannot affect totals |
| FCV-052 | high-confidence parser suggestion before confirmation | still excluded from authoritative records and totals |
| FCV-053 | confirmed valid draft | exactly one Income or Expense record contributes |
| FCV-054 | rejected confirmation | draft remains non-authoritative; no event or total |
| FCV-055 | manual valid record | authoritative and included without pretending AI produced it |
| FCV-056 | OCR/LLM changes a suggestion after record confirmation | confirmed record unchanged without an explicit deterministic correction |
| FCV-057 | transfer or unknown draft | not routed into Income/Expense totals |
| FCV-058 | summary rebuild input includes a draft/suggestion shape | contract rejected; projection unchanged |

## Summary and calculation invariants

| ID | Scenario | Expected assertion |
| --- | --- | --- |
| FCV-059 | active income and expense in one day | `balanceDelta = income - expense` exactly |
| FCV-060 | records span day/week/month | each inclusive period contains only matching dates |
| FCV-061 | no records | `200`, zero numeric totals, empty categories, stale with null last event |
| FCV-062 | projection lag exceeds threshold | last-known totals returned with `isStale = true` |
| FCV-063 | archived records remain in projection storage | excluded from calculations but available for later restore/rebuild |
| FCV-064 | category totals summed | per-category income/expense reconcile with monthly totals |
| FCV-065 | unsupported event version or type | terminal safe rejection; no partial projection |
| FCV-066 | verified shadow rebuild | atomic alias switch; readers never observe partial totals |

## Required fixtures

- fixed UTC clock plus dates covering leap day, DST transition, week, month, and year boundaries;
- two synthetic users, two supported currencies, and matching/mismatching category IDs;
- deterministic manual and confirmed records with revisions for create, update, archive, and restore;
- duplicate idempotency keys, event IDs, occurrence IDs, and out-of-order delivery sequences;
- malformed requests at every validation boundary;
- zero-record and stale-projection datasets;
- no real names, merchants, account data, receipt content, OCR text, or prompts.

## Implementation order and exit gate

1. Add missing unit cases FCV-001 through FCV-022 to owning validators.
2. Add concurrency/replay cases FCV-023 through FCV-030 using deterministic barriers.
3. Complete lifecycle/category/ownership cases FCV-031 through FCV-050.
4. Add authoritative-boundary and summary cases FCV-051 through FCV-066.
5. Add the gateway-to-summary P9 integration flow after the API host and local stack exist.

No test may weaken a production rule, bypass authentication, use a live paid
provider, sleep for synchronization, depend on execution order, or delete a valid
assertion to make CI green. The plan is complete when all IDs are automated or
linked to a later Jira ticket with an explicit owner and reason, full solution CI
is green, and privacy review confirms synthetic fixtures only.
