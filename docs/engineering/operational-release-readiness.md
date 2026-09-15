# Operational Release Readiness Checklist

Deployment update (FIN-271, 2026-09-15): the approved POC target is the
[Windows-native WPF installer](../architecture/windows-native-poc.md), without
Docker or WSL. Native installed-host evidence is required for deployment/E2E
acceptance. Existing Compose commands and in-memory checks below remain
developer/legacy evidence, not proof of the new installation path.

The [POC QA strategy](poc-qa-strategy.md) coordinates this P8 gate with the
independent backend, mobile, provider and P9 release checks.

Related Jira: FIN-200, parent FIN-36. This is the release-owner verification
checklist for P8, not a deployment approval. The [blank decision record](../delivery/operational-readiness-record.json)
starts `Blocked`, with no candidate, environment, approver or evidence. Closing
this definition ticket cannot authorize a release or mark runtime gates passed.

## Decision And Candidate

All seven required checks below must have `Pass` evidence for the exact candidate
build and approved environment. The required rollback.status must also be Pass,
with the artifact, configuration, procedure, owner and recovery evidence below.
`Fail`, `Blocked`, missing, stale, pending,
cancelled or unexpectedly skipped evidence means **No-Go**. Never derive a pass
from Jira Done, POC percentage, a source-file reference or another build's CI.

Record candidate commit and artifact digest, PR/actual merge, environment,
declared tester/capability scope, UTC verification time, release owner and backup,
exact-head CI, safe evidence references and final decision. The owner verifies
deployed artifact identity, not merely the source branch. Changes to artifact,
configuration, provider, trust boundary or scope invalidate affected approvals;
re-run affected tests and re-evaluate the full decision. Never copy secrets into
the record. Store real operational evidence and approvals in restricted storage.

Keep `candidateCommit` (the deployed source revision), `pullRequest` (URL),
`reviewedHead` and `actualMergeCommit` separate. Record each required CI run in
`exactHeadCi` with its head SHA, workflow/check name, run URL and attempt,
conclusion and completion time. Verify the run head equals `reviewedHead`, the
actual merge comes from the independently re-read merged PR, and the deployed
artifact digest maps to `candidateCommit`. Record the verified relationship
between the reviewed head and merge; if the deployed candidate differs, require
candidate build/test evidence as well. A PR head is not its provisional merge SHA.
The JSON is a manual evidence template; no automatic Go validator is supplied.

For first-user scope the seven checks are mandatory; `Not applicable` cannot
waive them. Optional components may be explicitly excluded from the candidate
inventory only through reviewed product/security/owner scope approval, with a
documented fallback and no misleading UI claims. Excluding a future optional
tool does not waive the baseline access/audit/availability requirements. Do not
silently narrow first-user product scope to make a checklist green.

## Required Checks

| ID / owner | Verify on candidate and environment | Required evidence / blocker |
| --- | --- | --- |
| OPS-READY-001 Health / platform and service owners | Expected service/dependency inventory; process-only liveness; required readiness; optional degradation; configured target coverage and fresh observations | Synthetic healthy/degraded/unavailable, timeout and missing-data results with source timestamps; missing target, stale/partial probe, unknown required readiness or placeholder zero blocks |
| OPS-READY-002 Logs / service owners and security | Stable structured event IDs/templates, safe correlation across HTTP/events, bounded labels and approved sinks/retention | Captured synthetic canary sweep over success/denial/failure, field allowlists and sink access/disposal proof; raw data, unbounded labels or unapproved export blocks |
| OPS-READY-003 Alerts / platform, source owners and release owner | Approved FIN-198 rules on real sources, windows/freshness, dedupe/recovery/maintenance, staffed primary/backup and destination | Synthetic trigger, delivery receipt, acknowledgement/escalation and recovery drill for required alerts; no source adapter/evaluator, unknown coverage, failed delivery or missing cost/privacy approval blocks |
| OPS-READY-004 Audit / Audit owner and security | End-to-end cataloged append from required producers, separate ingestion trust, admin query, idempotency/conflict rejection and retention | Synthetic producer-to-store-to-query evidence, unauthorized negatives, durable restart/restore and expiry verification; missing producer/audit sink, mutable replacement, leakage or unproven required persistence blocks |
| OPS-READY-005 Admin / gateway, Monitoring and web owners | Real identity validation, provisioned role, independent service trust, HTTPS/no-store and safe dashboard/session behavior | Browser and owner-endpoint negative tests, expiry/logout/race/refresh and failure-state evidence; forged roles, cached data, direct storage access, placeholder auth or fake healthy data blocks |
| OPS-READY-006 Support / service and support owners | Approved read-only FIN-197 workflow, case access, safe messages, actual retry/state evidence and unresolved-case handoff | Synthetic AI/OCR/notification tabletop plus required producer/recovery drill; blind replay, invented attempt/success, budget reset, sensitive public notes or unowned handoff blocks |
| OPS-READY-007 MCP / MCP owner and security | Explicit deployed tool/role inventory, internal trust, allowlisted owner APIs, auditable invocation, truthful availability and bounded output | Actual protocol discovery/invocation and negative auth/role/input tests, audit-failure behavior and downstream-unavailable checks; unregistered assumed tool, raw storage escape, unaudited access or misleading fallback blocks |

Evidence must satisfy the [FIN-199 scenario plan](observability-admin-test-plan.md)
at its stated level. Unit/contract tests are useful but do not replace approved-host
integration, browser, retention or response drills. Every required case records
pass/fail/blocked/not-run, owner, UTC time, exact build, expected/actual safe result
and restricted artifact reference/hash. Do not expose raw fixture content in CI.

## Current Evidence Gaps

These are repository-backed limitations and unverified deployment conditions as
of this definition, not fabricated outage reports or successful runtime tests.
Candidate owners must replace each applicable gap with evidence before Go:

- Health conventions and configured Monitoring probes exist. Actual deployment
  inventory, target coverage, ingress and source freshness are not established by
  their synthetic tests. Broker-wide totals lack per-queue age; absent numeric
  fields can default to zero and cannot certify healthy queues.
- Structured logging and correlation exist. Approved external sinks, retention,
  cardinality budgets and end-to-end canary capture are separate evidence; no
  paid exporter or collection infrastructure is enabled by this checklist.
- FIN-198 rules are definitions. No evaluator, durable incident state, paging
  destination, approved roster or successful response drill is established here.
  Pending CI grace never relaxes merge gates; the HTTP minimum is 20 requests over five minutes.
- Audit contracts and in-memory tests verify append/idempotency/privacy behavior.
  Verify the chosen persistent adapter and actual producer wiring; a code path or
  optional adapter is not proof of configured ingestion, durability or physical
  expiry. Pseudonymous actor/subject hashes remain restricted, not anonymous.
- Admin UI and endpoints exist, but deployment auth/provisioning/TLS and browser
  acceptance need actual evidence. Monitoring counters/jobs are process-local;
  the 200-entry/24-hour store loses observations on restart/eviction and producer
  wiring is not implied by the ingestion API. Empty is not proven healthy.
- FIN-197 support is read-only guidance. Operator support lookup stays disabled;
  case access, owner reconciliation, actual retry scheduling and recovery are
  not demonstrated by a runbook. Non-sending notification placeholders cannot
  count as delivered; provider/job/broker retries must not reset spend limits.
- The six current MCP tools are the baseline. FIN-196's four additional actions
  remain unregistered; do not require or claim them as enabled implicitly. If
  candidate scope needs them, approved owner adapters and separate implementation
  evidence are required. Current health lacks a freshness timestamp and cost/
  parsing fallback can return zeros when Monitoring is unavailable; do not use
  those outputs as healthy/no-activity evidence. Resolve or explicitly gate the
  affected capability with approved, truthful unavailable behavior before Go.

## Safe Verification Sequence

1. Declare candidate/scope, artifact identity and approved environment. Check
   privacy, access, cost and staffed support ownership before running host tests.
2. Run exact-head restore/build/test/format and required Backend/Mobile/Admin CI;
   inspect every review channel, label and scope under the existing merge gate.
3. Review environment-backed credentials by presence/reference, never values.
   Confirm service-specific least privilege, rotated trust and restricted ingress.
4. Execute offline checks, then explicitly approved synthetic host/browser tests.
   Verify timeouts, cancellation, missing data, denied access and recovery, not only
   happy paths. No production mutation, real provider call or paid send by default.
5. Fill each check with evidence and unresolved blockers. Verify current source
   timestamps for availability at decision time; no arbitrary old snapshot is a
   substitute. Re-run security/functional evidence after relevant changes.
6. Release owner reviews all seven checks and independent P6/P7/P9 gates, signs a
   dated Go/No-Go decision for the exact artifact and scope, and names rollback
   and support owners. A PR merge is not this approval.

Do not run destructive recovery, force replay, weaken auth or change retention
just to make a check pass. Use fake providers and synthetic jobs. Any external
provider, alert destination, exporter, cloud build or deployment requires explicit
privacy/credential/maximum-spend approval; default additional spend is zero.
Technical cost counters are neither an invoice nor approval to buy credits.

## Rollback And Release Boundary

Before Go, require an approved previous artifact/configuration, compatibility and
rollback procedure, source/audit preservation, recovery verification and named
owner. Never claim a backup works until a synthetic restore is verified. The
record has a separate required `rollback` gate; passing the seven area
checks cannot substitute for it. Record the previous artifact digest, approved
configuration and procedure references, owner, successful restore evidence and
recovery verification evidence before changing its status to Pass. Missing or
failed rollback evidence keeps the overall decision No-Go.
Rollback must not erase authoritative records or duplicate provider sends; uncertain
outcomes require owner reconciliation. A new rollback candidate gets its own
health/access/flow checks and decision record. Do not run rollback from this file.

P8 implementation scope and runtime acceptance are different. Even if all P8
leaf tickets are Done, evaluate FIN-36's actual DoD rather than auto-closing the
epic from counts. Missing required integrated operational evidence remains open.
P6 notification runtime, client journeys and P9 storage/deployment/security/store/
controlled-rollout gates independently block first-user testing. FIN-205 validates
storage, FIN-215 security, FIN-216 dashboards/alerts and FIN-218 final go/no-go.
This document neither certifies them nor changes their ownership.

Related contracts: [health](service-health-and-readiness.md),
[alert rules](alerting-and-incident-priorities.md),
[Audit](../api/audit-admin-v1.md), [Monitoring](../api/monitoring-admin-v1.md),
[admin UI](../../web-admin/monitoring-ui/README.md),
[support](failed-job-support-workflow.md), [MCP diagnostics](mcp-operational-diagnostics.md),
[P6 readiness](insights-release-readiness-checklist.md).
