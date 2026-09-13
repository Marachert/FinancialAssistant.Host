# Alerting Rules And Incident Priorities

Related Jira: FIN-198, parent FIN-36. Status: definition only. This extends the
[FIN-190 observability policy](../architecture/backend-observability-strategy.md),
not a deployed evaluator, collector, paging integration or on-call roster.
Thresholds are draft controlled-POC defaults requiring owner approval and synthetic
calibration. No paid provider, alert destination or automatic repair is enabled.
The [rule inventory](../delivery/alert-rules.json) is an offline coverage manifest,
not executable configuration. Its priority is the default; impact overrides below apply.

## Evidence And Availability

Use the authenticated, admin-only [Monitoring API](../api/monitoring-admin-v1.md)
and [owned readiness checks](service-health-and-readiness.md). Only required
dependencies affect core readiness; optional OCR/AI failure is not automatically
a core-product outage. Do not call paid providers to probe health.

- Service probes report configured `/health/ready` status and checked time.
  Liveness is process-only, not evidence of a healthy required store.
- RabbitMQ uses broker-wide `/api/overview` message/consumer totals, not per-queue
  oldest-message age or required-queue consumer count. Zero fallback values on
  failed/malformed probes are not recovery evidence; even healthy probes currently
  default absent numeric fields to zero. Backlog rules need a validated owner-side
  per-queue adapter, not an inference from these totals.
- Elasticsearch color is not proof of authoritative writes, projection correctness,
  audit persistence or backup restorability. Yellow alone is not data loss.
- Monitoring counters are process-local cumulative aggregates without complete
  per-service window history. Restart/replica changes and unwired producers make
  rates unknown. The 200-entry/24-hour job store is not a unique-job denominator.
  FIN-195 failure-revision buckets and log counts are not request error rates.
- Missing coverage, stale timestamps, unauthorized probes, timeout, partial results
  and invalid samples mean `unknown`, never zero or healthy. Snapshot generation
  time alone cannot establish producer freshness.

No owner adapter, durable evaluation state, deployment inventory or approved
destination is implemented by this ticket. Do not arm a rule until its source
and expected coverage are verified for that environment.

## Priority And Ownership

Role expectations are drafts, not an SLA or claimed 24/7 coverage. Before tester
rollout, the release owner must name a primary and backup, confirm staffed UTC
coverage and an approved destination, and test acknowledgement/escalation.
Absent coverage is a release blocker, not permission to invent an on-call person.

| Priority | Impact | Draft acknowledgement / update expectation |
| --- | --- | --- |
| P1 Critical | Financial correctness/security invariant breach, authoritative data loss risk, or confirmed broad core outage | Notify primary, backup, release and security owner immediately; acknowledge within 5 minutes and update every 15 minutes during staffed operation; stop release |
| P2 High | Sustained critical-service unavailability, blocked required processing, high request errors or exhausted provider/cost guardrail | Primary acknowledges within 15 minutes, updates every 30 minutes; notify backup/release owner when acknowledgement is overdue |
| P3 Medium | Optional feature degradation, increasing lag or missing coverage | Operator and source owner triage the same staffed day; acknowledge within 4 staffed hours, update by end of that day |
| P4 Low | Capacity/retry trend with no current impact | Owner reviews within 2 staffed business days and records backlog decision |

P1/P2 clocks start at first validated detection, not ticket creation. No
acknowledgement is not recovery: escalate to backup and release owner. Outside
approved coverage, halt new tester admission and use the agreed emergency contact
process; do not claim targets are met. Response targets are not restoration-time
promises. Suspected security or integrity risk gets immediate restricted owner
review without waiting for a numeric threshold.

## Rule Definitions

Proposed evaluation interval is 30 seconds, freshness limit 90 seconds from source
observation. Future timestamps more than 5 seconds ahead, out-of-order samples
and duplicate observation IDs cannot advance a window. Sustained conditions
require complete expected coverage. Missing samples interrupt pending threshold
accumulation and preserve firing incidents as unknown, never resolve them.

| Rule | Safe source and trigger | Priority / owner | Recovery evidence |
| --- | --- | --- | --- |
| `service_down` | Critical service readiness unavailable for two distinct consecutive 30-second probes; validate freshness and required scope | P2 owning service/platform; P1 for confirmed broad core impact | Three consecutive fresh healthy readiness samples plus owner verification of affected core flow |
| `provider_failure` | Owner-side terminal physical attempts: at least 20 attempts and at least 20% technical failures in each of five consecutive one-minute buckets; explicit disabled/configuration/cost guardrail evidence bypasses rate gating | P2 for exhausted guardrail or blocked required capability; P3 for partial optional loss; AI/OCR/notification owner | Five complete minutes below 10% with verified coverage and controlled successful work; zero traffic alone cannot resolve |
| `queue_backlog` | Required oldest pending age exceeds 300 seconds in a fresh owner sample, or pending messages with zero consumers persist for two probes; P3 for strictly increasing age across ten 30-second samples below P2 threshold | P2 required processing blocked, P3 growing lag; consumer owner/platform | Age below 60 seconds and expected consumers present for five minutes; verified empty queue counts only with complete fresh coverage |
| `http_error_rate` | One request boundary: at least 20 completed requests across the full rolling five-minute window and HTTP 5xx at least 5% over that same window; require complete source coverage, exclude health probes/client cancellations, count downstream failures once | P2 gateway/service owner | A complete five-minute window below 1% with at least 20 total requests and verified coverage; otherwise owner-approved synthetic flow evidence and manual resolution |
| `storage_failure` | Required owned persistence unavailable for two probes; confirmed lost authoritative event, corruption or financial invariant breach fires immediately | P2 availability, P1 integrity; store-owning service/platform and security when relevant | Fresh required readiness plus approved persistence/restore or integrity verification; never clear P1 from cluster color |
| `release_blocker` | For an exact candidate with release approval requested, any mandatory gate failed, pending, cancelled, unexpectedly skipped, missing or stale; before approval request, a check still pending/missing more than 30 minutes after candidate registration triggers review; missing coverage/destination approval blocks release | P2 release owner for requested release or overdue checks; P1 security/integrity immediately | Every required gate verified successful for unchanged candidate, approvals current, explicit release-owner go decision |
| `visibility_gap` | Expected armed source has no valid fresh sample for 90 seconds, is unauthorized, partial or not configured | P3 Monitoring/source owner; P2 and release block when critical evidence cannot be established | Three consecutive fresh complete samples; independently re-evaluate affected incidents before resolution |
| `integrity_security` | Validated owning-domain invariant or security-boundary failure, confirmed authoritative event loss or broad core outage; no window | P1 domain/security/release owners | Explicit containment and integrity acceptance with follow-up; no automatic clear |

Ordinary pending CI prevents merge but does not page during the 30-minute build
grace period before release approval is requested. Candidate registration records
the exact head/environment and UTC start; a new head supersedes the prior candidate
without claiming recovery of its failures. A normally completed failed build needs
owner follow-up, not automatic P2 paging unless release approval was requested or
verified runtime/security/integrity impact warrants it. Expired pending/missing
checks trigger P2 owner review, never automatic merge. No grace period relaxes the
merge gate: every mandatory check must succeed on the exact head.

For HTTP errors, four requests per minute with all requests failing yields
20/20 failures over five minutes and triggers P2. Nineteen total requests remain
insufficient, and zero requests never imply recovery. The threshold is inclusive:
one failure among 20 total requests is 5%; requests need not be evenly distributed
among minutes. This concretizes FIN-190's full-window minimum, not 20 per minute.

Rate numerator/denominator use the same boundary and window, with
`0 <= failed <= total`. Provider counts include physical bounded retries, not
logical jobs; policy-disabled calls are separate guardrail events, not technical
failures. Notification consent suppression and manual review are not provider
failure. Normal 4xx are not 5xx. Never combine gateway and service counts for one
request. Counter resets or replica discontinuity invalidate the window. Below
minimum traffic, evidence is insufficient; use readiness or verified owner
evidence, never divide by zero or page on a single synthetic rate.

Queue age is server-measured age of the oldest pending item, including retries
blocking required processing. Reviewed scheduled/deferred work has a separate
eligibility baseline. Never inspect payloads or accept arbitrary queue names.
Optional latency/dependency degradation remains P3 after 15 minutes under FIN-190;
non-impacting trends remain P4. Severity follows verified impact, not red-panel count.

## Lifecycle And Safe Response

Future states: `inactive`, `pending`, `firing`, `acknowledged`, `resolved`,
`unknown`. Unknown does not erase previous firing/acknowledged state, detection
time or acknowledgement timer. On evaluator restart, restore durable incident
state; otherwise require owner reconciliation, never silently reset incidents.

Deduplicate by approved environment, rule ID and technical component, never user,
financial entity, correlation ID, raw URL or free text. Suppress redundant
notifications for 15 minutes, never severity increases, overdue acknowledgements,
security escalation or new affected capability. Group verified dependency
cascades under one root incident while retaining child evidence and ownership;
do not suppress unrelated failures. Acknowledgement means ownership, not fixed.

Maintenance suppression needs named approval, exact environment/components,
reason, start/end UTC and maximum 60-minute expiry. It cannot suppress security
or integrity alerts or turn failed release gates green. Re-evaluate on expiry.
Silencing is not acknowledgement, resolution or release approval.

Alert messages allow rule ID, controlled service/environment, priority, first/last
UTC observation, safe condition, sample counts/coverage and approved fixed
dashboard/runbook links. No credentials, identities, financial amounts, receipts,
OCR text, prompts, provider responses, exceptions, raw queue payloads, internal
addresses or customer-linked identifiers. No secret/query payloads in links.
Real incident evidence stays in restricted storage, not public Jira/GitHub/Confluence.

Follow [failed-job support](failed-job-support-workflow.md). Read-only diagnostics
never authorize replay, budget reset, provider activation, database repair,
cross-service storage access or financial confirmation. Unknown acknowledgements
require owner reconciliation before retry. Use only separately approved owner
controls for containment/recovery, preserving evidence and idempotency. Close
with the rule's recovery evidence, safe impact summary and owner acceptance.

## Acceptance Before Arming

FIN-199 owns test-plan expansion; FIN-200/P9 own operational acceptance. Test
threshold edges, low/zero traffic, counter resets, duplicate/out-of-order samples,
stale/missing/partial data, restart, disabled providers, consent suppression,
maintenance expiry, deduplication, severity escalation, overdue acknowledgement
and recovery flapping. Use synthetic signals, no real providers or customer data.
Verify restricted routing and no alert path can mutate financial state or spend.

Record exact build/environment, owner/backup approval, source/schema/freshness,
threshold, synthetic trigger/recovery evidence, destination receipt, response
drill, retention/security review and maximum-spend approval (zero by default).
Offline manifest tests do not prove runtime delivery, coverage, correctness or
first-user readiness. FIN-216 must verify actual dashboards and alerts before
release; no rule is armed by merging this file.
