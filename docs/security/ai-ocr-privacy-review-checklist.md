# AI and OCR Privacy Review Checklist

## Purpose

FIN-117 defines the privacy review gate for data sent to external AI and OCR
providers. Complete this checklist before enabling a provider in a production
environment and repeat it when the provider, model, capability, request contract,
retention terms, processing region, or subprocessors change.

This checklist does not approve a provider by itself. It records the evidence that
Security, AI Orchestration, Receipt Processing, Product, and release reviewers need
to make an explicit decision. Provider output remains untrusted suggestion data and
cannot create a confirmed financial record.

The machine-readable source is:

```text
docs/security/ai-ocr-privacy-review-checklist.json
```

`AiOcrPrivacyReviewChecklistTests` validates the required privacy domains, stable
check IDs, raw-input storage policy, provider question set, release-readiness link,
and Markdown synchronization.

## Review Record

Record these values in the pull request or approved privacy-review system. Do not
copy raw prompts, receipt content, OCR text, provider responses, real identities, or
financial records into the review.

| Field | Required value |
| --- | --- |
| Capability | Named AI or OCR capability and owning service |
| Provider | Provider and model or OCR product identifier |
| Request contract | Versioned allowlisted request or mapping |
| Data classes | Minimized categories sent to the provider |
| Processing region | Approved region and cross-border path |
| Retention | Provider and owner-storage retention periods |
| Reviewers | Security, service owner, Product/privacy owner |
| Evidence | Tests, terms, diagrams, approvals, and deletion procedure |
| Review date | UTC date and next review trigger |

For every stable check below, record one decision:

- `pass`: evidence demonstrates the expected behavior;
- `fail`: the provider or implementation is blocked;
- `not_applicable`: the check genuinely does not apply to the named capability and
  the reviewer records the capability scope, rationale, approver, decision date, and
  compensating controls or an explicit statement that none are needed. This record
  is the substitute evidence for the check.

Any unresolved `fail`, unanswered provider question, missing required evidence for
a `pass`, missing substitute evidence for `not_applicable`, or unknown
retention/training behavior blocks production enablement. A later duplicate or
replacement provider inherits no approval automatically.

## Input Minimization

### AI-OCR-PRIVACY-001

Verify that the provider request contains only fields required for the named
capability.

Required review:

- enumerate every transmitted field and its purpose;
- use a dedicated provider request contract rather than serializing a domain object;
- prefer bounded normalized values over free-form or complete records;
- do not send confirmed records when suggestion input is sufficient;
- prove unrelated fields are omitted with a negative test.

Block when any field has no documented purpose or a less sensitive representation
would satisfy the capability.

## Provider Data Handling

### AI-OCR-PRIVACY-002

Answer and approve the provider data-handling questions:

1. What content does the provider store in request, response, abuse-monitoring, or
   support systems?
2. What is the default retention, can it be reduced or disabled, and how is the
   configured value verified?
3. Is submitted content used for model training, product improvement, or human
   review, including opt-out behavior?
4. Which subprocessors can access content and how are changes announced?
5. In which regions is content processed or stored, including cross-border
   transfers?
6. How are data encrypted in transit and at rest, and who can access them?
7. How is provider-held content deleted, what is the deletion SLA, and what evidence
   is returned?
8. What incident and breach notification commitments apply?

Unknown retention, training use, subprocessors, processing region, deletion
capability, or incident terms block production enablement.

### AI-OCR-PRIVACY-008

Verify that provider access can be disabled and provider-held data can be deleted:

- credentials can be revoked without a code change;
- disabled providers fail closed with a safe user status;
- deletion requests and escalations are traceable without embedding raw content;
- provider output remains suggestion data;
- provider disablement, failure, or deletion never confirms a financial record.

## Masking And Redaction

### AI-OCR-PRIVACY-003

Verify that unnecessary identifiers and metadata are removed before transmission:

- omit account, session, device, and confirmed-record identifiers;
- strip receipt file metadata that the OCR capability does not require;
- redact unrelated text or image regions when practical and approved;
- validate the minimized payload at the provider boundary;
- use irreversible redaction when correlation is not required.

Encoding, token prefixes, partial identifiers, and reversible masking are not
redaction. Synthetic before-and-after fixtures must demonstrate the rule without
using real receipt or financial data.

## Raw Input Storage

### AI-OCR-PRIVACY-004

Raw prompts, receipt bytes, and OCR text may exist only in approved, encrypted,
service-owned storage when the capability requires them.

Required controls:

- use opaque references in commands, events, drafts, and cross-service APIs;
- enforce owner-scoped authorization for every raw-content read;
- define bounded retention and an executable deletion path;
- prevent raw content from entering integration events, transaction drafts, logs,
  metrics, traces, tickets, Confluence evidence, or analytics;
- repeat deterministic validation after provider output is received.

Provider retention is separate from owner-storage retention. Both must be reviewed
and neither may be described as "temporary" without a defined duration.

## Logging

### AI-OCR-PRIVACY-005

Observability may contain only approved technical metadata such as stable job and
command identifiers, safe failure categories, attempt count, duration, provider or
model key, trace ID, and timestamps.

It must not contain:

- prompts, completions, receipt images, OCR text, or normalized receipt fields;
- provider request or response bodies;
- exception messages, stack traces, or provider error details;
- user, account, session, or device identifiers;
- amounts, merchants, categories, notes, balances, or other financial values;
- production content copied into Jira, Confluence, dashboards, or alerts.

Use the [safe operational log policy](../engineering/safe-operational-log-policy.md)
as the baseline and add a regression test whenever a new observable field is
introduced.

## Consent And Privacy Policy

### AI-OCR-PRIVACY-006

Before transmission, verify that applicable user consent and privacy disclosures
cover:

- the processing purpose and external provider category;
- the minimized data classes sent;
- provider and owner-storage retention or deletion expectations;
- processing region or cross-border transfer where applicable;
- withdrawal or disablement behavior;
- a safe manual or provider-disabled fallback.

Consent must be specific enough for the capability and recorded without storing the
submitted prompt or receipt. Withdrawal cannot remove authoritative financial
records, but it must stop future provider processing and trigger the approved
provider-data deletion path where required.

## Test Data

### AI-OCR-PRIVACY-007

Automated tests, fixtures, demos, screenshots, and troubleshooting artifacts use
only generated synthetic or irreversibly sanitized data.

Reviewers must confirm:

- fixture provenance is documented;
- no production export, real receipt, real prompt, or real provider response was
  used;
- synthetic identities and financial values cannot be traced to a real person;
- repository scans find no credentials or sensitive binary artifacts;
- failed tests and CI artifacts do not print raw fixture content unnecessarily.

## Release Readiness

The completed review is an input to Jira `FIN-124`, the AI and OCR release-readiness
checklist. FIN-124 must confirm that:

- the [AI and OCR integration test plan](../engineering/ai-ocr-integration-test-plan.md)
  passes on the release commit with generated synthetic fixtures;
- every enabled provider and capability has a current review;
- all decisions are `pass` or approved `not_applicable`;
- privacy policy and consent dependencies are complete;
- retention, deletion, fallback, monitoring, and support procedures are executable;
- provider configuration and cost controls match the reviewed capability.

A code review may verify implementation evidence, but it cannot waive an unresolved
privacy or provider-contract decision.

Record the final provider, consent, cost, fallback, monitoring, support, and
release-commit decisions in the
[FIN-124 AI and OCR release-readiness checklist](../engineering/ai-ocr-release-readiness-checklist.md).
