# AI and OCR Integration Test Plan

## Purpose

FIN-123 defines the integration and contract test plan for AI parsing, OCR
extraction, deterministic receipt normalization, and OCR-to-draft suggestion
delivery. It turns provider-dependent behavior into repeatable tests that use
mocked boundaries and generated synthetic data.

The machine-readable plan and fixture catalog are:

```text
docs/engineering/ai-ocr-integration-test-plan.json
```

`AiOcrIntegrationTestPlanTests` keeps the JSON, this document, existing test
evidence, and release-readiness link synchronized.

No test in the required pull-request lane may contact an external provider. A
provider, model, or adapter change also requires an explicitly approved sandbox
contract run before FIN-124 can mark that provider ready.

## Test Levels

| Level | Boundary | Required behavior |
| --- | --- | --- |
| Deterministic contract | Schema validator and receipt normalizer | Same synthetic input always produces the same validation or candidate result |
| Service contract | AI request/response and suggestion contracts | Output is schema-valid, suggestion-only, and review-required |
| Service integration | In-process API, storage, provider mock, audit store | Authentication, policy, persistence, failure, and privacy paths work together |
| Cross-service contract | `ocr.completed.v1` delivery into Transaction Intake | Authenticated, idempotent suggestion delivery creates no confirmed record |
| Provider boundary | Resilient AI/OCR wrappers around mocked adapters | Timeout, retry, cancellation, disabled, and unknown failures fail safely |
| Sandbox contract | Approved non-production provider and generated fixtures | Provider-specific request/response mapping passes without production data |

The sandbox level is not part of ordinary pull-request CI. It is an explicit
FIN-124 release input and must identify provider, model, adapter commit, approved
region, fixture IDs, UTC run time, and result without attaching raw payloads.

## Synthetic Fixtures

All fixtures are generated for FIN-123. None originated from a production export,
real receipt, real prompt, captured provider request/response, or identifiable
person. Numeric values, names, dates, images, and failure sequences are fictional.

| Fixture | Purpose |
| --- | --- |
| `SYN-AI-EXPENSE-001` | Valid expense suggestion with explicit ambiguity and review fields |
| `SYN-AI-MALFORMED-001` | Invalid JSON, schema, keyword, format, and date variants |
| `SYN-AI-LOW-CONFIDENCE-001` | Low-confidence suggestion that remains non-authoritative |
| `SYN-OCR-SINGLE-TOTAL-001` | Generated image signature and labeled OCR text with one total |
| `SYN-OCR-AMBIGUOUS-TOTAL-001` | Multiple totals, missing fields, and mismatched line items |
| `SYN-DRAFT-UPDATE-001` | Normalized suggestion-only `ocr.completed.v1` event |
| `SYN-PROVIDER-FAILURE-001` | Transient, permanent, timeout, cancellation, disabled, and unknown failures |

Fixture rules:

- use only generated text, identifiers, dates, amounts, and image bytes;
- keep raw AI input/output and OCR text inside the test process;
- do not emit fixture bodies to logs, TRX names, snapshots, Jira, or Confluence;
- store only safe expected metadata and stable fixture IDs in evidence;
- review every fixture change for personal data, credentials, and binary artifacts;
- never refresh a fixture from a production incident or provider transcript.

## Required Matrix

### AI-OCR-IT-001 AI Parsing Contract

Use `SYN-AI-EXPENSE-001` at the service-contract boundary.

- valid output satisfies the registered schema;
- ambiguity and missing fields remain explicit;
- output authority is `suggestion` and review is mandatory;
- authority fields, invalid confidence, and invalid dates fail closed;
- no response can carry confirmed financial state.

Existing evidence:

- `NaturalLanguageTransactionContractTests`
- `TransactionParsingPromptCatalogTests`

### AI-OCR-IT-002 Malformed Provider Response

Use `SYN-AI-MALFORMED-001` through the validator and orchestration service.

- invalid JSON or schema output never reaches the caller;
- malformed registered schemas and unsupported keywords/formats fail closed;
- impossible calendar dates fail validation;
- audit metadata records only a bounded failure category;
- raw output and provider exception details are absent from persistence.

Existing evidence:

- `JsonSchemaStructuredOutputValidatorTests`
- `AiOrchestrationServiceTests`

### AI-OCR-IT-003 Low-Confidence Suggestion

Use `SYN-AI-LOW-CONFIDENCE-001` at the parsing contract.

- low confidence stays visible on the suggestion and fields;
- missing and ambiguous values remain reviewable;
- values outside zero through one are rejected;
- low confidence never confirms or mutates an authoritative record.

Existing evidence:

- `NaturalLanguageTransactionContractTests`
- `TransactionParsingPromptCatalogTests`

### AI-OCR-IT-004 OCR Extraction Fixture

Use `SYN-OCR-SINGLE-TOTAL-001` through Receipt Processing with a mocked
`IOcrProviderClient`.

- generated image bytes are encrypted at rest;
- tampered stored ciphertext fails authenticated decryption;
- request size and daily quota pass before object decryption;
- the mock receives the expected bytes and content type;
- raw image and OCR text are absent from stored audit metadata;
- spoofed signatures, oversized requests, exhausted quota, and disabled providers
  make no external call.

Existing evidence:

- `ReceiptEndpointTests`
- `EncryptedReceiptObjectStoreTests`

### AI-OCR-IT-005 Receipt Normalization

Use `SYN-OCR-SINGLE-TOTAL-001` and `SYN-OCR-AMBIGUOUS-TOTAL-001` directly
against the deterministic normalizer.

- labeled candidates normalize deterministically;
- multiple or repeated totals remain explicit ambiguity;
- impossible dates are excluded;
- missing and mismatched line-item fields remain placeholders;
- unlabeled raw OCR text is not copied into candidate fields.

Existing evidence:

- `ReceiptCandidateNormalizerTests`

### AI-OCR-IT-006 Draft Suggestion Update

Use `SYN-DRAFT-UPDATE-001` across the authenticated `ocr.completed.v1`
contract.

- one event creates one reviewable Transaction Intake draft;
- same-event redelivery is idempotent;
- conflicting redelivery and mismatched ownership are rejected;
- the event and draft use opaque receipt correlation without raw OCR text;
- neither delivery nor replay creates a confirmed financial record.

Existing evidence:

- `OcrCompletedEventEndpointTests`
- `OcrBoundaryTests`

### AI-OCR-IT-007 Provider Failures

Use `SYN-PROVIDER-FAILURE-001` at both resilient provider boundaries.

- only explicit transient failures retry within configured bounds;
- timeouts apply even when an adapter ignores cancellation;
- caller cancellation is preserved without retry;
- unknown provider details map to safe categories;
- disabled providers make no external call;
- every failure leaves authoritative financial state unchanged.

Existing evidence:

- `ResilientLlmProviderTests`
- `ResilientOcrProviderTests`

## Pull-Request Execution

Run the required provider-free lane:

```powershell
dotnet restore FinancialAssistant.Backend.sln
dotnet build FinancialAssistant.Backend.sln --no-restore --configuration Release
dotnet test FinancialAssistant.Backend.sln --no-build --configuration Release --logger trx --results-directory TestResults
dotnet format FinancialAssistant.Backend.sln --verify-no-changes --verbosity diagnostic
```

The lane passes only when:

- every required test succeeds on the current head;
- no test is skipped unexpectedly;
- no network provider credential is required;
- test output contains no fixture body, secret, personal data, or raw provider data;
- safe metadata, suggestion authority, and user-review assertions remain present.

## Provider Sandbox Contract Run

Run a sandbox contract only when an adapter exists and the provider/privacy review
permits the named capability. Use an isolated non-production account, generated
fixture IDs from this plan, a bounded request count, and environment-injected
credentials. Never use production endpoints, identities, receipts, prompts, or
financial records.

The run must verify:

- request mapping sends only the approved capability fields;
- response mapping passes the same deterministic schema and authority checks;
- timeout, rate-limit, malformed-response, and safe-error behavior match the
  provider contract;
- configured provider/model identity and usage metadata match the run;
- provider retention, region, training, and deletion settings match the approved
  privacy record;
- evidence records only IDs, versions, safe categories, counts, timestamps, and
  pass/fail results.

Any mismatch blocks that provider/model without weakening the mocked pull-request
lane.

## FIN-124 Handoff

FIN-124 may mark AI/OCR integration testing ready only when:

- `AI-OCR-IT-001` through `AI-OCR-IT-007` pass on the release commit;
- fixture provenance remains generated-synthetic-only;
- no required check is skipped or quarantined;
- every enabled provider/model has a current approved sandbox contract result;
- privacy, provider configuration, cost controls, fallback, and monitoring evidence
  refer to the same capability and provider/model;
- unresolved malformed-output, low-confidence, normalization, draft-delivery, or
  provider-failure result blocks release;
- provider output remains suggestion-only and deterministic confirmation tests pass.

The linked inputs are the
[privacy review checklist](../security/ai-ocr-privacy-review-checklist.md),
[provider configuration baseline](ai-ocr-provider-configuration.md), and
[usage cost controls](ai-ocr-usage-cost-controls.md).

Record the final decisions and matching release identity in the
[FIN-124 AI and OCR release-readiness checklist](ai-ocr-release-readiness-checklist.md).
