# Asynchronous AI and OCR processing flow

## Purpose

AI parsing and OCR extraction are provider-dependent jobs. Public financial APIs accept
work, persist an owned request or receipt, and expose status; workers perform provider calls
after the REST request has completed. Provider output remains suggestion data and cannot
confirm a financial record.

This document defines the versioned boundary contracts, lifecycle, retry policy, and
permanent-failure behavior. Broker transport configuration remains environment-owned.

## Responsibilities

| Boundary | Responsibility |
| --- | --- |
| Public REST | Authenticate, validate bounded input, enforce idempotency, persist owned state, return the resource and current status. |
| Event publisher | Publish from durable owned state and retry publication without duplicating the business resource. |
| Job dispatcher | Convert a source event into one idempotent provider job command. |
| AI or OCR worker | Load source data through an authenticated service boundary, call the provider, validate and normalize output, and publish status plus one terminal event. |
| Transaction Intake | Apply suggestion data to a reviewable draft and expose user-visible status. |
| Confirmation REST flow | Revalidate the reviewed draft deterministically and publish `transaction.confirmed.v1`; it never runs from an AI or OCR event. |

Raw natural-language input, prompts, receipt bytes, OCR text, provider payloads, and
exception details are not copied into commands or events. Contracts carry opaque owned
references, normalized suggestion references, safe failure categories, and correlation
identifiers.

## Job lifecycle

The common lifecycle is:

```text
queued -> processing -> suggestion_ready
                    \-> failed
failed -> queued
```

- `queued` means the source event was accepted and a job command is available.
- `processing` means one worker owns the current attempt.
- `suggestion_ready` is terminal for that job and points to validated suggestion data.
- `failed` is terminal for the attempt. `Retryable` says whether policy may enqueue another
  attempt; it does not schedule the retry itself.
- A retry keeps the same `JobId`, increments `Attempt`, creates a new command identifier,
  and transitions from `failed` to `queued`.

Consumers deduplicate source events by `EventId`, commands by `CommandId`, and the logical
operation by `JobId`. Status consumers reject stale attempts and invalid transitions.

## Retry policy

Provider-call retries and asynchronous job retries are separate layers:

- a provider adapter may perform its configured short, in-process retries within one job
  attempt;
- after those retries are exhausted, the worker classifies the safe failure category and
  lets the job scheduler decide whether another job attempt is allowed;
- the scheduler permits at most three job attempts in total;
- attempt 2 is scheduled 30 seconds after attempt 1 fails;
- attempt 3 is scheduled 2 minutes after attempt 2 fails;
- broker scheduling may add up to 20 percent positive jitter, but may not shorten the base
  delay or exceed the three-attempt limit.

Transient categories are `provider_timeout`, `provider_unavailable`, `rate_limited`, and
`transport_failure`. Permanent categories include invalid input, invalid or unsafe provider
output, disabled providers, and unclassified provider failures. Unknown categories fail
closed and are never retried automatically.

For a retryable failure with attempts remaining, the worker publishes the existing failed
event, a failed status update, and then one retry-scheduled event:

- `ai.parsing-retry-scheduled.v1`; or
- `ocr.extraction-retry-scheduled.v1`.

The retry event contains the next command ID, failed and next attempt numbers, scheduled
time, safe failure category, user message code, provider/model identifiers, and trace ID.
It contains no raw input, prompt, receipt content, OCR text, provider response, exception
message, or stack trace.

## Permanent failure

A failure is permanent when its category is not retryable or attempt 3 fails. The worker:

1. publishes the existing failed event with `Retryable = false`;
2. publishes status `failed`;
3. publishes `ai.parsing-permanently-failed.v1` or
   `ocr.extraction-permanently-failed.v1`;
4. prevents any further automatic retry for that `JobId`;
5. routes the exhausted command to the restricted dead-letter queue after the terminal
   events are durably recorded.

User APIs expose only a safe localized message selected by message code:

| Message code | Default safe message |
| --- | --- |
| `processing_temporarily_delayed` | Processing is delayed. We will retry automatically. |
| `processing_failed` | We could not process this item. You can try again. |
| `processing_provider_disabled` | Automatic processing is currently unavailable. |

Operator metadata is limited to job and command identifiers, attempt count, safe failure
category, provider/model identifiers, trace ID, and timestamps. Raw provider details stay
inside the provider boundary and are not copied to events, user responses, Jira, or
Confluence evidence.

Recommended dead-letter queues are `ai.parsing.dead-letter.v1` and
`ocr.extraction.dead-letter.v1`. They require restricted operator access, encryption in
transit and at rest, bounded retention, queue-depth and oldest-message alerts, and an
audited replay tool. Replay after remediation creates a new job and command rather than
changing the terminal history of the failed job.

## AI parsing sequence

1. Transaction Intake accepts a natural-language draft request, stores the sensitive payload
   behind an opaque reference, and publishes `transaction.draft-created.v1`.
2. The AI dispatcher consumes the draft event and publishes `ai.parsing.requested.v1`.
3. The AI worker publishes `ai.parsing-status-updated.v1` with `processing`, resolves the
   payload through the authenticated owner boundary, and calls AI Orchestration.
4. Valid schema-checked output is stored as suggestion data. The worker publishes
   `ai.suggestion-ready.v1`, then status `suggestion_ready`.
5. A safe provider or validation failure publishes `ai.parsing-failed.v1`, then status
   `failed`. Retryable failures publish `ai.parsing-retry-scheduled.v1`; terminal failures
   publish `ai.parsing-permanently-failed.v1`. Transaction Intake maps the message code to
   safe localized user text.

## OCR extraction sequence

1. Receipt Processing accepts and securely stores a receipt, then publishes
   `receipt.uploaded.v1`.
2. The OCR dispatcher consumes the upload event and publishes
   `ocr.extraction.requested.v1`.
3. The OCR worker publishes `ocr.extraction-status-updated.v1` with `processing`, loads the
   receipt through owned storage, and calls the configured OCR provider.
4. Valid normalized candidates publish the existing `ocr.completed.v1` suggestion event,
   then status `suggestion_ready`.
5. A safe extraction or validation failure publishes `ocr.extraction-failed.v1`, then status
   `failed`. Retryable failures publish `ocr.extraction-retry-scheduled.v1`; terminal
   failures publish `ocr.extraction-permanently-failed.v1`. The receipt status endpoint
   maps the message code to safe localized user text.

## REST and event separation

Create and upload endpoints do not wait for provider completion in the production flow.
They return the accepted resource with `queued` status. Status reads are synchronous and
side-effect free. Events and commands perform provider work and update owned processing
state. Development transports may execute consumers in-process, but must preserve the same
contracts, idempotency keys, lifecycle, and privacy boundaries.

Suggestion events cannot call confirmation. A user must review the draft and invoke the
authenticated confirmation REST endpoint. Deterministic validation is repeated there before
any authoritative income or expense event is published. Retry, permanent-failure, and
dead-letter flows cannot invoke confirmation or create confirmed financial records.
