# AI and OCR Usage Cost Controls

## Purpose

FIN-121 defines a PoC cost-control baseline around external AI and OCR calls.
The controls prevent oversized or excessive per-user requests from reaching a
provider, retain privacy-safe usage metadata for reporting, and make provider
budget review an explicit release gate.

These controls do not calculate authoritative currency spend. Provider pricing,
discounts, retries, cached tokens, pages, and regional billing rules change
outside this repository. The provider invoice or approved billing export
remains the source of truth for spend.

## Configuration

AI settings use the `AiOrchestration:Provider:UsageCostControls` section:

| Environment variable | Development default | Allowed range |
| --- | ---: | ---: |
| `AiOrchestration__Provider__UsageCostControls__PerUserDailyRequestLimit` | 20 | 1-10,000 |
| `AiOrchestration__Provider__UsageCostControls__MaximumRequestCharacters` | 8,000 | 1-100,000 |
| `AiOrchestration__Provider__UsageCostControls__MonthlyBudgetAlertUsd` | 25 | 1-1,000,000 |
| `AiOrchestration__Provider__UsageCostControls__AdminVisibilityEnabled` | `true` | must be `true` |

OCR settings use the `ReceiptProcessing:Ocr:UsageCostControls` section:

| Environment variable | Development default | Allowed range |
| --- | ---: | ---: |
| `ReceiptProcessing__Ocr__UsageCostControls__PerUserDailyRequestLimit` | 10 | 1-10,000 |
| `ReceiptProcessing__Ocr__UsageCostControls__MaximumProviderRequestBytes` | 10,485,760 | 1-10,485,760 |
| `ReceiptProcessing__Ocr__UsageCostControls__MonthlyBudgetAlertUsd` | 25 | 1-1,000,000 |
| `ReceiptProcessing__Ocr__UsageCostControls__AdminVisibilityEnabled` | `true` | must be `true` |

Invalid limits or disabled admin visibility fail startup. The checked-in values
are non-secret PoC placeholders, not production budgets.

## Enforcement

AI callers provide a safe opaque `UsageSubjectId`. It is used only as the
in-memory daily counter partition and is not copied into call metadata. Request
size is the combined .NET string-character count of the capability, model,
registered prompt template, caller input, and registered output schema sent
through the provider boundary.

OCR uses the authenticated gateway user identifier already owned by Receipt
Processing. Provider request size comes from stored receipt metadata, before
the encrypted object is opened for OCR.

For each enabled provider:

1. reject a request above the configured character or byte limit;
2. atomically reserve one logical daily request for the user and provider;
3. call the provider only after both controls pass;
4. record one logical provider request unit for an attempted external call;
5. record zero provider units for size rejection, daily-limit rejection, or a
   disabled provider.

AI rejection raises a safe `AiUsageCostControlException` with
`provider_request_too_large` or `daily_usage_limit_exceeded`. OCR records an
`ocr_failed` result with the same safe failure categories and does not publish
an OCR-completed candidate.

Retries inside a provider adapter remain one logical user request. Actual
provider attempts, token billing, page billing, and spend must be reconciled
with provider billing data.

## Usage Metadata

AI call metadata includes:

- provider and model;
- status and safe failure category;
- input/output token counts when the provider supplies them;
- request character count;
- logical provider request units;
- UTC billing month;
- processing timestamps and duration.

OCR audit metadata includes:

- provider and model;
- status, confidence, and safe failure category;
- provider request bytes;
- logical provider request units;
- UTC billing month;
- processing duration and trace identifiers.

Raw prompts, AI input/output, receipt bytes, extracted text, provider responses,
credentials, exception messages, and stack traces remain prohibited.

## Admin Visibility and Alerts

The P8 admin surface must aggregate service-owned metadata by UTC billing month,
provider, model, and status. At minimum it must show:

- logical request units and rejected requests;
- AI token counts and request characters;
- OCR request bytes;
- success and failure counts by safe category;
- configured monthly alert threshold;
- provider-billed spend and data freshness when an approved billing source is
  integrated.

Recommended operator alerts:

- warning at 80% of the approved provider monthly budget;
- blocking review at 100%;
- disable the affected provider through FIN-118 configuration when spend cannot
  be reconciled or approved.

No automated currency alert is emitted in FIN-121 because there is no approved
provider billing feed. Inferring spend from request counts would create a false
financial source of truth.

## Disabled and Unavailable Providers

AI registers no capability route while its provider is disabled. OCR resolves
the disabled client, records a safe provider-disabled failure, consumes zero
external request units, and creates no confirmed financial record. Clients must
keep the existing manual draft-entry and retry-later experience available.

## PoC Persistence Limitation

Daily counters are race-safe but in-memory and process-local. They automatically
remove previous-day partitions, but restart with the process and do not
coordinate multiple replicas. This is acceptable only for the single-instance
PoC.

Before production or horizontal scaling:

- replace each limiter through its application interface with a durable,
  atomic, service-owned shared adapter;
- define retention and deletion for per-user counter partitions;
- reconcile logical usage metadata with the approved provider billing source;
- verify warning and blocking alerts;
- restrict aggregate usage views to the authenticated admin role;
- complete privacy, cost, fallback, monitoring, and support review.
