# Financial Score Formula v2

Related Jira: FIN-29, FIN-131.

Financial Score Service derives a transparent personal score from confirmed Income and Expense lifecycle events plus explicit Profile settings. Income, Expense, and Profile remain authoritative; the score projection is disposable and rebuildable.

## Deterministic formula

Formula version `financial-score-v2` starts at 50, rounds factor contributions to two decimals, then rounds and clamps the final result to 0 through 100.

| Factor | Range | Rule |
| --- | ---: | --- |
| Budget usage | -20 to +15 | Current calendar-month confirmed expense divided by the Profile monthly budget. An unconfigured budget contributes 0; usage through 80% contributes +15, 80-100% declines linearly to 0, 100-150% declines linearly to -20, and higher usage remains -20. |
| Spending trend | -10 to +10 | Current 30-day confirmed expense compared with the preceding 30 days. Lower spending is positive. With no preceding expense, zero current expense contributes 0 and positive current expense contributes -5. |
| Income consistency | 0 to +15 | Mean absolute deviation of confirmed income across the current and two preceding calendar months. At least two months with income are required; stable income approaches +15. |
| Data completeness | 0 to +10 | Up to 6 points for distinct confirmed-record days, capped at 30 days; 2 for a configured monthly budget; and 1 each for completed profile and preference onboarding. |
| Penalty and cap | policy | Expense without any confirmed income applies -15 and caps the score at 39. Monthly budget usage at or above 150% caps the score at 49. The stricter cap wins. |

All calculations use a 90-day confirmed-record observation window. Archived and out-of-window records do not contribute, currencies are never mixed, and input order cannot change the result.

## New-user behavior

A user with no confirmed financial records receives the neutral score 50. Profile completeness is reported in explanation inputs but does not move the score until at least one confirmed record exists. A first expense with no income uses the explicit expense-without-income penalty and cap. Missing budget or incomplete onboarding contributes no positive completeness points and never causes an exception.

## Explanation inputs

Every factor stores a stable code, numeric contribution, safe explanation, and factual typed inputs. Inputs include only aggregates and booleans such as monthly expense, budget, trend totals, months with income, confirmed-record days, and applied caps. They exclude raw user IDs, record IDs, categories, merchants, receipt/OCR content, prompts, and provider output.

## Profile settings boundary

`IFinancialScoreProfileSettingsProvider` is the application boundary for Profile-owned monthly budget and onboarding settings. The checked-in in-memory adapter is a local POC synchronization target and defaults to unconfigured settings. A durable deployment must populate the boundary from an authorized Profile API or minimal profile-settings event; it must not query Profile storage directly.

## Probabilistic boundary

The formula does not invoke an LLM and rejects non-empty semantic score adjustments at the application boundary. No model or external provider can submit a final score, change deterministic financial inputs, or bypass penalty and cap policies.

## Event projection and history

The RabbitMQ consumer owns `fa.financial-score.financial-events.v1` and binds confirmed income and expense created, updated, archived, and restored events. Record type, record ID, owner hash, currency, status, and revision form the idempotent projection boundary. Replay can republish its stored deterministic score event without reverting current projection state. Currency moves recalculate old and new scopes independently.

Each accepted revision appends one history item and publishes `score.calculated.v1`. The event contains the formula version and factor contributions, while trusted current/history APIs additionally expose safe explanation inputs.

## POC persistence

The default projection/history store, Profile-settings provider, and event publisher are in-memory POC adapters. Production requires durable implementations preserving revision, event identity, formula version, and Profile ownership. The default mode uses no paid services.
