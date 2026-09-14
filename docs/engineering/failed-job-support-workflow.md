# Failed Job Support Workflow

Related Jira: FIN-197, parent FIN-36. This defines a read-only MVP support
runbook for AI parsing, OCR extraction and notification delivery. It does not
enable providers, a scheduler, a replay endpoint or customer-data access.

## Current Capabilities

Validation scenarios and evidence requirements are in the
[observability and admin test plan](observability-admin-test-plan.md).

For incident severity, owner response targets and signal limitations, follow
[Alerting rules and incident priorities](alerting-and-incident-priorities.md).

Use the protected [Monitoring snapshot/jobs API](../api/monitoring-admin-v1.md)
and [Audit contract](../api/audit-admin-v1.md) only within approved access scope.
Monitoring has at most 200 latest observations for 24 hours in process-local
memory. Missing signals, restart and eviction create visibility gaps. A failed
observation is not a durable queue item or proof of a failed financial operation.

The admin UI support lookup remains disabled. The four additional
[MCP diagnostics](mcp-operational-diagnostics.md) are definitions, not registered
tools. Existing AI/OCR retry-policy contracts do not establish a deployed durable
scheduler. Notification push/web adapters remain non-sending placeholders;
`prepared` or `retry-scheduled` is not provider-confirmed delivery. Follow the
[current implementation map](../architecture/current-implementation.md) before
making a runtime claim. No raw storage/provider fallback is permitted.

## Triage Sequence

1. Authenticate and verify the admin role, approved environment and support-case
   purpose. For an individual case, require independently approved owner access;
   possession of a correlation or job identifier is not authorization.
2. Check aggregate health and source freshness. If unavailable, stale, partial or
   missing, record `visibility_gap` and involve the service owner. Never infer
   success from empty observations or zero fallback counters.
3. Identify kind and owning service: AI = AI Orchestration, OCR = Receipt
   Processing, notification = Recommendations/Notifications. Use only a safe
   owner API projection; do not inspect receipt bytes, raw input or queue payloads.
4. Verify latest owned state, attempt/revision and schedule through an approved
   bounded API. Monitoring revision is not the provider-attempt number. Its random
   operational UUID is not automatically a domain JobId or Audit correlation.
5. Classify using the owner's safe failure category and explicit transient flag.
   The coarse Monitoring categories `timeout`, `transport`, `provider_unavailable`,
   `invalid_result`, `policy_rejected` are diagnostic hints, not retry permission.
6. Apply the decision table below. Check cumulative attempt/cost budget, consent,
   cancellation, current configuration and idempotency before any owner-approved
   scheduling. Support itself has no mutation capability in this MVP runbook.
7. Send a safe message matching verified state. Record minimum necessary notes
   in restricted case storage, not public Jira, GitHub or Confluence.
8. Re-read authorized state before closing. Record evidence or an explicit
   unresolved owner handoff, not a fabricated successful recovery.

## Retry And Escalation Decisions

| Evidence | Support decision | Escalation |
| --- | --- | --- |
| AI/OCR transient allowlisted failure, remaining attempts, confirmed schedule | Wait for the owner's existing scheduled attempt; do not enqueue another | Owner if schedule is missing/stale or execution exceeds its configured deadline |
| Timeout or lost acknowledgement with unknown result | Check owner state/idempotency first; no blind resubmission | Owner reconciles ambiguous completion; no duplicate transaction or notification |
| Provider disabled, unconfigured, placeholder, quota/budget exhausted | No retry or automatic enablement; offer manual draft flow when available | Configuration/product owner; separate consent and financial approval required |
| Invalid input/content | No automatic retry; authenticated user may correct input through normal UX | Owning service if valid synthetic input is rejected |
| Invalid/unsafe provider output or unknown failure | No job-level automatic retry based on category alone | AI/OCR owner; security/privacy owner immediately for suspected disclosure |
| Attempts exhausted, permanent failure or restricted dead letter | Stop; preserve terminal history and sanitized evidence | Owner remediation; reviewed replay policy is a separate gate |
| Caller cancellation or consent revoked | Do not retry or reactivate work | Owner if state still suggests pending execution |
| Notification suppressed by preference/channel policy | Expected non-delivery; no retry and no preference override | Owner only for a verified policy mismatch |
| Notification explicitly transient and bounded retry scheduled | Observe the owner's schedule, not a support-created send | Owner when schedule is absent or cap reached |
| Notification delivered | No resend, including after uncertain client acknowledgement | Investigate only approved user-visible inconsistency |
| Stale/missing/partial observations | Record visibility gap, not success or failure | Monitoring plus source owner; no raw-data diagnostic fallback |

Notification receipt/opening is different from provider delivery acknowledgement.
Never claim a person received/read a message from `delivered` alone. Do not mark
an incident resolved simply because a later observation was evicted.

### Three Independent Retry Layers

| Layer | Existing contract | Support constraint |
| --- | --- | --- |
| In-process provider call | AI `transaction.parse` policy currently caps at two total calls; OCR defaults to two, configurable from one to three. Per-attempt timeout defaults to 30 seconds and is bounded to 1-120 seconds | Only explicit transient failures within approved adapter settings; cancellation is preserved. Never raise limits to clear an incident |
| AI/OCR logical job | At most three attempts; attempt 2 after 30 seconds, attempt 3 after 120 seconds; optional positive jitter cannot shorten base delay | Requires retryable category plus transient flag. Same JobId, new CommandId, incremented attempt; eligibility does not itself schedule work |
| Notification adapter | Default three total attempts with fixed 30-second delay; validated configuration supports 1-10 attempts and 1-3600-second delay | Only explicitly transient failed result; never retry suppression, missing config, placeholder or permanent rejection |

AI/OCR job retry categories are exactly `provider_timeout`,
`provider_unavailable`, `rate_limited`, `transport_failure`, and still require
the concrete failure's transient flag. `provider_disabled` is not transient.
Do not map Monitoring `provider_unavailable` back to that flag automatically.

Sources: [AI provider boundary](ai-provider-client-boundary.md),
[OCR pipeline](receipt-upload-ocr-pipeline.md),
[async job contracts](async-ai-ocr-processing-flow.md), and
[notification adapter policy](notification-delivery-adapters.md).
Broker redelivery (including notification event delays of 5 seconds, 30 seconds
and 5 minutes) is transport recovery, not authorization for another provider send.
Malformed contracts follow the owner's dead-letter policy, not repeated execution.

Limits across layers multiply. A future three-attempt AI job with two provider
calls per attempt could make six external attempts, excluding transport duplicate
effects. This is a planning bound, not an approved spend allowance or proof of
current scheduling. Durable deduplication, outbox state, cancellation and total
cost ceilings must prevent broker redelivery or support action resetting budgets.
An ambiguous provider acknowledgement must be reconciled before any resend.

### Manual Recovery Boundary

There is no operator retry/replay action in the current admin/MCP surface. Do not
invent a curl/SQL/queue command, reset counters or change terminal records. After
remediation, a future reviewed owner replay workflow must require exact target,
expected state/version, dry run, approved scope/cost, idempotency, audit and
post-action verification. AI/OCR replay uses a new job/command and preserves failed
history; it must not duplicate a draft or invoke financial confirmation. Automatic
retry within an existing job follows its own same-JobId policy above.

Users may use existing manual draft review/entry and explicit confirmation.
Before suggesting another upload or submission after an ambiguous response,
have the normal authenticated app verify existing state. Support never confirms
transactions, alters balances/scores or changes notification preferences.

## User-Safe Messages

These are support templates, not a change to runtime localization or registered
message codes. Select language through the normal product localization process.

| Verified condition | Suggested message |
| --- | --- |
| Retry actually scheduled | Processing is delayed. A retry is scheduled. You can check its status in the app. |
| AI cannot prepare a suggestion | Automatic entry is unavailable right now. You can review or enter the details manually. |
| OCR cannot prepare a suggestion | We could not extract the receipt details. You can enter the details manually. |
| Final failure | Processing could not be completed. Check the item status before submitting it again. |
| Unknown outcome | We cannot confirm the latest processing status yet. Please check the existing item before trying again. |
| Notification failed | We could not deliver this notification. Check the app for available updates. |
| Notification suppressed | Notifications are off for this channel. You can review your preferences in the app. |

Use existing `processing_temporarily_delayed` only when an actual schedule is
verified; a retryable flag alone is insufficient. Never promise that no charge,
no stored data, no duplicate, a specific recovery time or successful delivery is
known unless separately verified. Never ask users to send receipts, account
numbers, credentials, raw prompts, provider outputs or payment details to support.

## Operator Notes And Closure

Use the [synthetic case example](../delivery/failed-job-support-case.example.json)
as a minimal shape, not a production case store or tool input. Case references
must be independently generated opaque values, not lookup selectors. Real case
notes require approved restricted storage/access/retention; do not save them in
this repository. Store only kind, owner, safe category, freshness, verified
attempt/schedule state, decision, message choice, handoff and sanitized verification.
Unknown values stay null; never invent attempts or recovery evidence.

No free-text payload, actor/subject hash, customer/receipt/domain identifier,
destination, provider model output, attachment, stack trace, URL with secrets or
financial value belongs in default notes. Public engineering evidence includes
only synthetic reproductions, build/commit references and aggregate categories.
Do not paste raw DLQ messages into issue trackers or model chats.

Close as recovered only after fresh owner evidence shows the expected safe state
and the user-facing flow is verified without mutation by support. Record expected
suppression separately from recovery. Unresolved work is handed off with category,
owner and next check condition; suspected security/privacy impact goes immediately
to the approved incident process. FIN-198 owns severity/alert thresholds, FIN-199
the observability test plan, and FIN-200/P9 runtime release acceptance.

## Verification Scope

Repository tests check this runbook's source links, retry-layer distinction,
safe-message caveats and the synthetic note allowlist. No runtime recovery or
real delivery is claimed. Before rollout, exercise synthetic transient/permanent,
disabled, exhausted, cancelled, ambiguous, stale, suppression and unsafe-output
cases; assert no extra send, financial confirmation, identifier leakage or budget
reset. A definition ticket cannot by itself make the first-user environment ready.
