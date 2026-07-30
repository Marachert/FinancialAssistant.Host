# AI and OCR Release Readiness Checklist

## Purpose

FIN-124 defines the final review gate for AI and OCR capabilities before first
public release or app store publication. The checklist does not approve a provider
by itself. It binds privacy, consent, provider configuration, cost, reliability,
fallback, monitoring, and support evidence to the exact release commit and
deployment identity.

The machine-readable contract is:

```text
docs/engineering/ai-ocr-release-readiness-checklist.json
```

`AiOcrReleaseReadinessChecklistTests` verifies the required domains, dependencies,
stable check IDs, evidence paths, explicit blockers, and documentation links.

## Decision Record

Create one record for each release environment and every enabled
capability/provider/model combination. Record:

- release commit and build;
- environment and publication target;
- capability, provider, model, endpoint identity, and adapter commit;
- `pass`, `blocked`, or approved `not_applicable` for every check;
- privacy-safe evidence references, decision date, owner, and approver;
- the final zero-blocker release decision.

`pass` requires every item listed as required evidence for that check.
Missing required evidence blocks a `pass`. `not_applicable` instead requires
capability scope, rationale, approver, decision date, and compensating controls or
an explicit statement that none are needed.
Missing this substitute evidence blocks `not_applicable`. Any `blocked` decision or
unresolved blocking condition stops publication. A check from another provider,
model, capability, environment, or commit is not transferable evidence.

Shared release evidence may contain stable IDs, versions, counts, safe categories,
timestamps, pass/fail results, and references to access-controlled operational
records. It must not contain credentials, raw prompts, provider responses, receipt
content, OCR text, personal data, or user financial values. Approved provider
budgets and provider-billed spend are required operational cost evidence, but their
amounts remain in an access-controlled billing or operations system; pull requests,
Jira, Confluence, and broadly shared support evidence record only the approved
reference, status, owner, and freshness.

## Required Inputs

| Jira | Input | Release use |
| --- | --- | --- |
| FIN-117 | [Privacy review checklist](../security/ai-ocr-privacy-review-checklist.md) | Provider handling, consent, policy, retention, deletion, and safe evidence |
| FIN-118 | [Provider configuration](ai-ocr-provider-configuration.md) | Environment, capability, provider, model, endpoint, adapter, secret reference, and rollback |
| FIN-121 | [Usage cost controls](ai-ocr-usage-cost-controls.md) | Per-user and request limits, budget approval, admin visibility, and billing reconciliation |
| FIN-123 | [Integration test plan](ai-ocr-integration-test-plan.md) | Release-commit regression results and provider sandbox contract evidence |

Implementation tests can support a decision, but cannot waive missing product,
privacy, provider-contract, budget, monitoring, or support approval.

## Checklist

### AI-OCR-READY-001

**Provider configuration.** The service owner confirms that every enabled
capability uses one approved environment, provider, model, HTTPS endpoint, adapter
commit, bounded resilience policy, and deployment-injected credential reference.
Provider or model changes require renewed privacy, cost, and sandbox review.

Block release when an adapter is missing or mismatched, a credential value appears
in source or evidence, or deployment identity does not match the approval.

### AI-OCR-READY-002

**Provider privacy.** Security/privacy confirms data minimization, retention,
training use, subprocessors, processing region, deletion, incident notification,
and privacy-safe operational evidence using the FIN-117 checklist.

Block release for any failed or missing required decision, unknown provider term,
unapproved retention/deletion path, or raw AI/OCR content in shared telemetry or
evidence.

### AI-OCR-READY-003

**Privacy policy and consent.** Product/privacy confirms that the published policy
discloses external processing and that capability-specific consent is obtained
before transmission. Withdrawal must stop future provider calls and preserve a
safe non-provider path.

Block release when consent can be bypassed, disclosures are incomplete, withdrawal
does not disable future processing, or no manual fallback remains.

### AI-OCR-READY-004

**Cost limits.** Product/operations approves per-user limits, maximum request size,
monthly budget thresholds, provider billing reconciliation, and authenticated admin
visibility for the release environment. Budget and billed-spend amounts remain in
the approved access-controlled billing or operations system.

Block release when the production budget or owner is missing, configured limits
exceed approval, usage is not visible, or provider-billed spend cannot be
reconciled. Also block when operational amounts are copied into a public or broadly
shared evidence channel. Checked-in PoC thresholds are not production budget
approval.

### AI-OCR-READY-005

**Fallback user experience.** The following behavior is required:

- disabled, unavailable, quota-rejected, or failed AI processing leaves manual
  draft entry available;
- OCR failure leaves the receipt workflow reviewable with manual draft entry and a
  clear retry-later action;
- low-confidence or ambiguous output remains visibly unconfirmed and editable;
- retry is explicit and idempotent, and cannot duplicate a draft or silently spend
  through repeated provider calls;
- user-facing errors use safe categories and never expose provider details or
  sensitive content;
- no fallback path confirms or partially persists an authoritative financial
  record.

Block release if any capability becomes unusable without its provider, retry can
duplicate work, sensitive detail reaches the user, or suggestion-only authority is
lost.

### AI-OCR-READY-006

**Integration and provider contract tests.** `AI-OCR-IT-001` through
`AI-OCR-IT-007` must pass on the release commit with generated synthetic fixtures.
Each enabled provider/model also needs a current approved sandbox contract result
matching its adapter and privacy record.

Block release for any failed, skipped, quarantined, stale, or mismatched result, real
or provider-captured test data, or a path that bypasses deterministic validation and
user confirmation.

### AI-OCR-READY-007

**Monitoring.** The P8 admin and monitoring workstream must give authenticated
operators privacy-safe visibility by environment, capability, provider, and model.
Required signals are request units, rejections, latency, safe failure category,
timeouts, quota state, token/request-byte totals, budget threshold, billing data
freshness, and provider-disable/escalation triggers.

Block release when failure, quota, or budget state is invisible; access is not
admin-protected; alerts expose sensitive content; or operators have no tested
disable/escalation trigger. Existing audit metadata is an input, not proof that the
operator surface and alerts are complete.

### AI-OCR-READY-008

**Support and troubleshooting.** Support/operations owns a versioned runbook that
uses only safe correlation IDs and categories. It covers common symptoms,
retry/manual guidance, provider disablement, budget or quota exhaustion, escalation,
provider deletion requests, privacy incidents, service ownership, and user
communication. Validate it with synthetic evidence.

Block release when troubleshooting depends on raw prompts, provider responses,
receipt content, or OCR text; disablement and escalation are untested; or a privacy,
deletion, or user-impact case has no accountable owner.

### AI-OCR-READY-009

**Final release binding.** The release owner rechecks every source immediately
before publication and records the exact commit, environment, capability, provider,
model, decisions, approvers, and evidence.

Block release when any required check remains blocked, a `not_applicable` decision
lacks substitute evidence, evidence does not match the deployment identity, or
configuration changed after approval.

## Current Baseline And Explicit Blockers

The repository supplies safe configuration, privacy, cost-control, integration-test,
audit-metadata, and provider-failure contracts. That is implementation evidence, not
a declaration that public release is ready.

Until release-specific evidence proves otherwise, publication remains blocked by:

- selection and approval of each real provider, model, adapter, and production
  environment;
- completed provider terms, privacy-policy, consent, retention, and deletion
  decisions;
- approved production budgets and provider billing reconciliation;
- end-to-end fallback acceptance for each user-facing capability;
- P8 authenticated monitoring, dashboards, and verified budget/failure alerts;
- a tested support and troubleshooting runbook with escalation ownership;
- durable production storage, delivery, and usage-limit adapters where the current
  service documentation identifies in-memory PoC implementations;
- current release-commit tests and provider-specific sandbox contract results.

These are public-release blockers. Internal PoC testing may use disabled or approved
sandbox providers only when its separate test scope, privacy decision, synthetic-data
policy, and cost boundary are documented.
